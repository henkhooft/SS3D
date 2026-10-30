using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet;
using SS3D.Core;
using SS3D.Permissions;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Character;
using SS3D.Systems.PlayerControl;
using SS3D.Systems.Rounds;
using SS3D.Systems.Rounds.Messages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.Editor.Automation
{
    /// <summary>
    /// Host-mode lobby session smoke: ready/unready → admin start → spawn → stop → return,
    /// late join, and local client disconnect/reconnect. Callable through MCP execute_menu_item.
    /// <para>
    /// Remote mode launches the built client (Builds/Game) as a non-admin player running
    /// Testing/multiplayer/scenarios/lobby-remote-client.txt, and drives the admin side from this Host.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class LobbyLifecyclePilot
    {
        private const string ActiveKey = "SS3D.LobbyLifecyclePilot.Active";
        private const string OutputKey = "SS3D.LobbyLifecyclePilot.Output";
        private const string SceneKey = "SS3D.LobbyLifecyclePilot.PreviousStartScene";
        private const string ModeKey = "SS3D.LobbyLifecyclePilot.Mode";
        private const string RemoteMode = "remote";
        private const string RemoteCkey = "remoteprobe";
        private const string ClientBinary = AutomationBuild.ClientBinary;
        private const string RemoteScenario = "Testing/multiplayer/scenarios/lobby-remote-client.txt";

        private static readonly List<string> Errors = new();
        private static readonly List<string> Checks = new();
        private static readonly List<string> Timeline = new();
        private static Step[] _steps;
        private static int _index;
        private static bool _entered;
        private static double _stepDeadline;
        private static double _started;
        private static bool _running;
        private static string _savedName;
        private static System.Diagnostics.Process _client;
        private static Report _pending;
        private static int _osInputsDone;
        private static string _clientWindow;
        private const string ChatProbe = "remoteprobe real keyboard";

        private sealed class Step
        {
            public string Name;
            public Action Enter;
            public Func<bool> Until;
            public Action Verify;
            public double Timeout = 30;
        }

        static LobbyLifecyclePilot()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("SS3D/Testing/Run Lobby Lifecycle Pilot")]
        public static void Run() => Start("lifecycle");

        [MenuItem("SS3D/Testing/Run Lobby Remote Client Pilot")]
        public static void RunRemoteClient() => Start(RemoteMode);

        private static bool Remote => SessionState.GetString(ModeKey, "") == RemoteMode;

        private static void Start(string mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before starting the lobby lifecycle pilot.");
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed)
                throw new InvalidOperationException("Fix compilation errors before starting the lobby lifecycle pilot.");
            if (SessionState.GetBool(ActiveKey, false)) RestoreEditorSettings();
            string output = Path.GetFullPath($"artifacts/ui/{(mode == RemoteMode ? "remote" : "lifecycle")}-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(output);
            try
            {
                AutomationPreflight.Require($"lobby {mode} pilot");
                // Testing/ui/validate.sh rebuilds first; a direct run refuses rather than test an old client.
                string stale = mode == RemoteMode ? AutomationBuild.ClientStaleReason() : null;
                if (stale != null)
                    throw new InvalidOperationException($"Refusing the remote client pilot: {stale}. "
                        + "Rebuild with Testing/ui/validate.sh or AutomationBuild.BuildClient().");
            }
            catch (InvalidOperationException exception)
            {
                Write(new Report { status = "failed", failure = exception.Message }, output);
                throw;
            }
            SessionState.SetString(OutputKey, output);
            SessionState.SetString(ModeKey, mode);
            SessionState.SetString(SceneKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Content/Scenes/Boot.unity");
            File.WriteAllText(Path.Combine(output, "status.json"), "{\"status\":\"starting\"}");
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Errors.Clear();
                Checks.Clear();
                Timeline.Clear();
                _steps = Remote ? BuildRemoteSteps() : BuildSteps();
                _index = 0;
                _entered = false;
                _started = EditorApplication.timeSinceStartup;
                _osInputsDone = 0;
                _clientWindow = null;
                _running = true;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (_running) Finish("Play Mode exited before the scenario completed.", false);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreEditorSettings();
                if (_pending == null) return;
                Report report = _pending;
                string output = OutputDir;
                _pending = null;
                AutomationPreflight.AfterPlay(problems => Complete(report, output, problems));
            }
        }

        private static void RestoreEditorSettings()
        {
            string previous = SessionState.GetString(SceneKey, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous)
                ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(ActiveKey, false);
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Errors.Add($"[{Elapsed():0.0}s step {_index}] {message}\n{stack}");
        }

        private static double Elapsed() => EditorApplication.timeSinceStartup - _started;

        private static void Tick()
        {
            if (!_running) return;
            try
            {
                if (Remote) PerformOsInput();
                Step step = _steps[_index];
                if (!_entered)
                {
                    _entered = true;
                    _stepDeadline = EditorApplication.timeSinceStartup + step.Timeout;
                    step.Enter?.Invoke();
                    return;
                }

                if (step.Until != null && !step.Until())
                {
                    if (EditorApplication.timeSinceStartup > _stepDeadline)
                        throw new TimeoutException($"Step '{step.Name}' timed out after {step.Timeout}s. State: {Describe()}");
                    return;
                }

                step.Verify?.Invoke();
                Timeline.Add($"{Elapsed():0.0}s {step.Name}: {Describe()}");
                _entered = false;
                if (++_index >= _steps.Length) Finish(null, true);
            }
            catch (Exception exception) { Finish(exception.ToString(), true); }
        }

        private static Step[] BuildSteps() => new Step[]
        {
            new Step
            {
                Name = "Lobby ready on host boot",
                Timeout = 180,
                Until = () => Online() && Local() != null && Rounds() != null && Visible(Shell()) && Rounds().CurrentRoundState == RoundState.Stopped
                    && CountLabel() == "0/1 ready" && ReadyButton().enabledInHierarchy,
                Verify = () =>
                {
                    MonoBehaviour[] legacy = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(behaviour => behaviour.GetType().Name == "LobbyScreen").ToArray();
                    Require(legacy.Length > 0 && legacy.All(b => !b.gameObject.activeInHierarchy), "Legacy LobbyScreen exists and is suppressed");
                    Require(!Visible(Creator()), "Creator hidden on boot");
                    Require(BoothLive(), "Preview booth active with RT bound");
                    Require(AdminButton().enabledInHierarchy, "Host owner has Admin enabled");
                    Require(ReadyButton().enabledInHierarchy && ReadyButton().text == "NOT READY", "Ready button enabled and NOT READY");
                    Require(CountLabel() == "0/1 ready", "Sidebar shows 0/1 ready");
                    _savedName = CharacterName();
                },
            },
            new Step { Name = "Ready", Enter = () => Click(ReadyButton()), Until = () => IsReady() && ReadyButton().text.Contains("READY") && CountLabel() == "1/1 ready",
                Verify = () => Require(ReadyButton().text == "✓  READY", "Ready reflected in button and count") },
            new Step { Name = "Unready", Enter = () => Click(ReadyButton()), Until = () => !IsReady() && CountLabel() == "0/1 ready",
                Verify = () => Require(ReadyButton().text == "NOT READY", "Unready reflected in button and count") },
            new Step { Name = "Ready again", Enter = () => Click(ReadyButton()), Until = IsReady },
            new Step
            {
                Name = "Open creator before round start",
                Enter = () => Click(Shell().Q(className: "lobby-shell__preview")),
                Until = () => Visible(Creator()) && !Visible(Shell()),
            },
            new Step
            {
                Name = "Round start spawns ready player and hides lobby",
                Enter = () => InstanceFinder.ClientManager.Broadcast(new ChangeRoundStateMessage(true)),
                Timeout = 120,
                Until = () => Rounds().CurrentRoundState == RoundState.Ongoing && Spawned() && !Visible(Shell()) && !Visible(Creator()),
                Verify = () => Require(!BoothLive(), "Booth deactivated while in world"),
            },
            new Step
            {
                Name = "Stop round returns to lobby shell",
                Enter = () => InstanceFinder.ClientManager.Broadcast(new ChangeRoundStateMessage(false)),
                Timeout = 30,
                Until = () => Rounds().CurrentRoundState == RoundState.Stopped && !Spawned() && Visible(Shell()),
                Verify = () =>
                {
                    Require(!Visible(Creator()), "Return shows shell, not creator");
                    Require(BoothLive(), "Booth restored with RT bound");
                    Require(CharacterName() == _savedName, "Saved character name restored");
                },
            },
            new Step { Name = "Ensure unready for late join", Enter = () => { if (IsReady()) Click(ReadyButton()); }, Until = () => !IsReady() },
            new Step { Name = "Open admin tab", Enter = () => Click(AdminButton()), Until = () => FindButton(Shell(), "START ROUND") != null },
            new Step
            {
                Name = "Admin Start Round leaves unready player in lobby",
                Enter = () => Click(FindButton(Shell(), "START ROUND")),
                Timeout = 120,
                Until = () => Rounds().CurrentRoundState == RoundState.Ongoing && ReadyButton().text == "JOIN ROUND",
                Verify = () =>
                {
                    Require(!Spawned() && Visible(Shell()), "Unready player stays in lobby");
                    Require(ReadyButton().enabledInHierarchy, "Join Round enabled while Ongoing");
                },
            },
            new Step
            {
                Name = "Late join spawns",
                Enter = () => Click(ReadyButton()),
                Until = () => Spawned() && !Visible(Shell()),
                Verify = () => Require(!BoothLive(), "Booth deactivated after late join"),
            },
            new Step
            {
                Name = "Stop round after late join returns",
                Enter = () => InstanceFinder.ClientManager.Broadcast(new ChangeRoundStateMessage(false)),
                Until = () => Rounds().CurrentRoundState == RoundState.Stopped && !Spawned() && Visible(Shell()),
                Verify = () => Require(BoothLive(), "Booth restored after second return"),
            },
            // New connections 60s+ after server start used to time out before their first packet.
            new Step { Name = "Wait past client timeout window", Timeout = 90, Until = () => Elapsed() > 70 },
            new Step
            {
                Name = "Client disconnect hides lobby",
                Enter = () => InstanceFinder.ClientManager.StopConnection(),
                Until = () => !InstanceFinder.ClientManager.Started && !Visible(Shell()) && !Visible(Creator()),
                Verify = () => Require(!BoothLive(), "Booth deactivated offline"),
            },
            new Step
            {
                Name = "Client reconnect restores lobby",
                Timeout = 60,
                // Like the recovery overlay's Retry: a start issued while Tugboat is still stopping is dropped.
                Until = () => RetryConnection() && Online() && Local() != null && Visible(Shell()) && CountLabel() == "0/1 ready" && ReadyButton().enabledInHierarchy,
                Verify = () =>
                {
                    Require(BoothLive(), "Booth restored after reconnect");
                    Require(CountLabel() == "0/1 ready", "Player list repopulated after reconnect");
                    Require(ReadyButton().enabledInHierarchy, "Ready usable after reconnect");
                    Capture("lobby-after-reconnect");
                },
            },
            new Step { Name = "Ready after reconnect", Enter = () => Click(ReadyButton()), Until = IsReady },
        };

        // The client script (lobby-remote-client.txt) asserts its own UI; these steps drive the admin side
        // and check the server's view. Each wait mirrors a client step, so a client failure surfaces here too.
        private static Step[] BuildRemoteSteps() => new Step[]
        {
            new Step
            {
                Name = "Host lobby ready",
                Timeout = 180,
                Until = () => Online() && Local() != null && Rounds() != null && Visible(Shell()) && Rounds().CurrentRoundState == RoundState.Stopped
                    && CountLabel() == "0/1 ready",
                Verify = () =>
                {
                    Require(AdminButton().enabledInHierarchy, "Host owner has Admin enabled");
                    Require(!SubSystems.Get<PermissionSubSystem>().IsAtLeast(RemoteCkey, ServerRoleTypes.Administrator), $"'{RemoteCkey}' has no Administrator permission");
                },
            },
            new Step
            {
                Name = "Remote client joins lobby",
                Timeout = 120,
                Enter = LaunchClient,
                Until = () => ClientAlive() && RemotePlayer() != null && CountLabel() == "0/2 ready",
                Verify = () => Require(!RemoteSpawned(), "Remote player joins unspawned"),
            },
            new Step
            {
                Name = "Remote non-admin start ignored, remote readies via UI",
                Timeout = 90,
                Until = () => ClientAlive() && RemoteReady() && CountLabel() == "1/2 ready",
                Verify = () =>
                {
                    Require(Rounds().CurrentRoundState == RoundState.Stopped, "Non-admin start request did not start a round");
                    Require(ClientSignal("UiText", ".lobby-shell__countdown-sub Stopped"), "Client observed Stopped after its start request");
                    Require(!IsReady(), "Host stays unready");
                    Require(_osInputsDone >= 1 && !ClientLog().Contains("UiSubmitted"), "Remote readied through a real OS click, not a synthetic submit");
                },
            },
            new Step
            {
                Name = "Remote sends chat with real keyboard input",
                Timeout = 60,
                Until = () => ClientAlive() && ClientLog().Contains("Chat send (") && ClientLog().Contains(ChatProbe),
                Verify = () => Require(_osInputsDone >= 5, "Chat tab click, field click, typing and Return performed as OS input"),
            },
            new Step { Name = "Open admin tab", Enter = () => Click(AdminButton()), Until = () => FindButton(Shell(), "START ROUND") != null },
            new Step
            {
                Name = "Admin Start Round spawns ready remote only",
                Enter = () => Click(FindButton(Shell(), "START ROUND")),
                Timeout = 120,
                // UI snapshots trail round state by the 200ms refresh: wait for the label too.
                Until = () => Rounds().CurrentRoundState == RoundState.Ongoing && RemoteSpawned() && ReadyButton().text == "JOIN ROUND",
                Verify = () =>
                {
                    Require(!Spawned() && Visible(Shell()), "Unready host stays in lobby");
                    Require(ReadyButton().text == "JOIN ROUND", "Host shows Join Round while Ongoing");
                },
            },
            new Step
            {
                Name = "Admin Stop Round returns remote",
                Enter = () => Click(FindButton(Shell(), "STOP ROUND")),
                Until = () => Rounds().CurrentRoundState == RoundState.Stopped && !RemoteSpawned() && CountLabel() == "0/2 ready",
            },
            // The client must see Stopped and its shell before the next round starts.
            new Step { Name = "Remote shell restored", Timeout = 60, Until = () => !ClientAlive() || ClientLog().Contains("remote-04-returned.png") },
            new Step
            {
                Name = "Second round: remote late-joins via Join Round",
                Enter = () => Click(FindButton(Shell(), "START ROUND")),
                Timeout = 120,
                Until = () => Rounds().CurrentRoundState == RoundState.Ongoing && RemoteSpawned(),
                Verify = () => Require(ClientSignal("UiText", ".lobby-shell__btn--ready JOIN ROUND"), "Remote saw Join Round before spawning"),
            },
            new Step
            {
                Name = "Stop second round",
                Enter = () => Click(FindButton(Shell(), "STOP ROUND")),
                Until = () => Rounds().CurrentRoundState == RoundState.Stopped && !RemoteSpawned(),
            },
            new Step
            {
                Name = "Remote script completes and disconnects",
                Timeout = 90,
                // PlayerSubSystem keeps offline players, so wait on the online count, not the record.
                Until = () => ClientLog().Contains("Test signal \"ScriptComplete\"") && CountLabel() == "0/1 ready",
                Verify = () =>
                {
                    string log = ClientLog();
                    Require(!log.Contains("Test signal \"ScriptFailed\""), "Remote script did not fail");
                    // Environment.Exit can stall in a windowed player's teardown (the harness kills leftovers too).
                    Timeline.Add(_client.HasExited ? $"client exited with code {_client.ExitCode}" : "client still tearing down after ScriptComplete; killed");
                    string[] remoteErrors = log.Split('\n').Where(line => line.Contains("Exception") || line.Contains("[Error]") || line.Contains("[ERR]")).ToArray();
                    foreach (string line in remoteErrors) Errors.Add("[remote] " + line);
                    Capture("host-after-remote");
                },
            },
        };

        private static string OutputDir => SessionState.GetString(OutputKey, "");

        private static string ClientLogPath => Path.Combine(OutputDir, "client.log");

        private static void LaunchClient()
        {
            ushort port = InstanceFinder.TransportManager.Transport.GetPort();
            System.Diagnostics.ProcessStartInfo info = new()
            {
                FileName = Path.GetFullPath(ClientBinary),
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(ClientBinary)),
                Arguments = $"-ip=127.0.0.1 -port={port} -ckey={RemoteCkey} -skipintro -testscript=\"{Path.GetFullPath(RemoteScenario)}\" "
                    + $"-screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile \"{ClientLogPath}\"",
                UseShellExecute = false,
            };
            info.Environment["SS3D_AUTOMATION_OUTPUT_DIR"] = OutputDir;
            _client = System.Diagnostics.Process.Start(info);
            Timeline.Add($"{Elapsed():0.0}s launched client pid {_client.Id} on port {port}");
        }

        private static bool ClientAlive()
        {
            if (_client == null) return false;
            if (_client.HasExited && _index < _steps.Length - 1)
                throw new InvalidOperationException($"Remote client exited early (code {_client.ExitCode}). See {ClientLogPath}");
            return !_client.HasExited;
        }

        // The client writes its log while running; open shared to read it mid-run.
        private static string ClientLog()
        {
            if (!File.Exists(ClientLogPath)) return "";
            using FileStream stream = new(ClientLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

        // TestSignal renders through Serilog, which quotes string properties.
        private static bool ClientSignal(string signal, string payload) =>
            ClientLog().Contains($"Test signal \"{signal}\" \"{payload}\"");

        private static Player RemotePlayer() => SubSystems.TryGet(out PlayerSubSystem players) ? players.GetPlayer(RemoteCkey) : null;

        private static bool RemoteReady() => RemotePlayer() != null && SubSystems.TryGet(out ReadyPlayersSubSystem ready) && ready.IsReady(RemotePlayer());

        private static bool RemoteSpawned() => RemotePlayer() != null && SubSystems.TryGet(out EntitySubSystem entities) && entities.IsPlayerSpawned(RemotePlayer());

        // The client emits OsClick "x y selector" / OsType "text" / OsKey "keysym"; perform each once, in order,
        // as real X11 input on its window. Clicking focuses the window, so typing goes to it.
        private static void PerformOsInput()
        {
            if (_client == null || _client.HasExited) return;
            System.Text.RegularExpressions.MatchCollection requests = OsRequest.Matches(ClientLog());
            for (; _osInputsDone < requests.Count; _osInputsDone++)
            {
                string kind = requests[_osInputsDone].Groups[1].Value;
                string payload = requests[_osInputsDone].Groups[2].Value;
                _clientWindow ??= ClientWindow();
                string[] parts = payload.Split(' ');
                string args = kind switch
                {
                    "OsClick" => $"mousemove --window {_clientWindow} {parts[0]} {parts[1]} click 1",
                    "OsType" => $"type --delay 40 -- \"{payload}\"",
                    _ => $"key -- {payload}",
                };
                Xdotool(args);
                Timeline.Add($"{Elapsed():0.0}s OS input {kind} {payload}");
            }
        }

        private static readonly System.Text.RegularExpressions.Regex OsRequest = new("Test signal \"(OsClick|OsType|OsKey)\" \"(.*)\"");

        private static string ClientWindow()
        {
            string[] windows = Xdotool($"search --onlyvisible --pid {_client.Id}").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (windows.Length == 0) throw new InvalidOperationException($"No visible X11 window for client pid {_client.Id}");
            return windows[^1].Trim();
        }

        // Needs xdotool and an X display (Unity under XWayland on DISPLAY=:0); moves the real pointer.
        private static string Xdotool(string arguments)
        {
            System.Diagnostics.ProcessStartInfo info = new("xdotool", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(info);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(10000) || process.ExitCode != 0)
                throw new InvalidOperationException($"xdotool {arguments} failed: {error}");
            return output;
        }

        private static void StopClient()
        {
            if (_client != null && !_client.HasExited) _client.Kill();
            _client = null;
        }

        private static double _nextRetry;

        private static bool RetryConnection()
        {
            if (!Online() && EditorApplication.timeSinceStartup >= _nextRetry)
            {
                _nextRetry = EditorApplication.timeSinceStartup + 2;
                InstanceFinder.ClientManager.StartConnection();
            }

            return true;
        }

        private static bool Online() => InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Started;

        private static Player Local() => Online() && SubSystems.TryGet(out PlayerSubSystem players)
            ? players.GetPlayer(InstanceFinder.ClientManager.Connection) : null;

        private static RoundSubSystem Rounds() => SubSystems.TryGet(out RoundSubSystem rounds) ? rounds : null;

        private static bool IsReady() => Local() != null && SubSystems.TryGet(out ReadyPlayersSubSystem ready) && ready.IsReady(Local());

        private static bool Spawned() => Online() && SubSystems.TryGet(out EntitySubSystem entities)
            && entities.IsPlayerSpawned(InstanceFinder.ClientManager.Connection);

        private static VisualElement Root() => UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
            .Select(d => d.rootVisualElement)
            .FirstOrDefault(r => r != null && r.Q("lobby-shell-surface") != null);

        private static VisualElement Shell() => Root()?.Q("lobby-shell-surface");

        private static VisualElement Creator() => Root()?.Q("character-creator-surface");

        private static bool Visible(VisualElement surface) => surface != null && surface.resolvedStyle.display != DisplayStyle.None;

        private static Button ReadyButton() => Shell().Q<Button>(className: "lobby-shell__btn--ready");

        private static Button AdminButton() => Shell().Q<Button>(className: "lobby-shell__btn--admin");

        private static string CountLabel() => Shell().Q<Label>(className: "lobby-shell__countdown")?.text;

        private static string CharacterName() => Shell().Q<Label>(className: "lobby-shell__character-name")?.text;

        private static Button FindButton(VisualElement surface, string text) =>
            surface?.Query<Button>().Where(b => b.text == text && b.worldBound.width > 0).First();

        private static bool BoothLive()
        {
            CharacterPreviewBooth booth = UnityEngine.Object.FindAnyObjectByType<CharacterPreviewBooth>();
            if (booth == null || booth.PreviewTexture == null) return false;
            bool camera = booth.GetComponentsInChildren<Camera>(true).Any(c => c.enabled);
            VisualElement image = Shell()?.Q(className: "lobby-shell__preview-image");
            return camera && image != null && image.resolvedStyle.backgroundImage.renderTexture == booth.PreviewTexture;
        }

        private static string Describe()
        {
            RoundSubSystem rounds = Rounds();
            return $"online={Online()} local={Local() != null} round={(rounds != null ? rounds.CurrentRoundState.ToString() : "-")} "
                + $"ready={IsReady()} spawned={Spawned()} shell={Visible(Shell())} creator={Visible(Creator())} "
                + $"button='{(Shell() != null ? ReadyButton()?.text : "-")}' count='{(Shell() != null ? CountLabel() : "-")}'";
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message + " | " + Describe());
            Checks.Add(message);
        }

        // Button.clicked ignores synthetic ClickEvents; buttons get NavigationSubmit, other elements ClickEvent.
        private static void Click(VisualElement element)
        {
            Require(element != null && element.enabledInHierarchy && element.worldBound.width > 0, "Clickable element is enabled and laid out");
            if (element is Button button)
            {
                button.Focus();
                using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
                submit.target = button;
                button.SendEvent(submit);
                return;
            }

            using ClickEvent evt = ClickEvent.GetPooled();
            evt.target = element;
            element.SendEvent(evt);
        }

        private static void Capture(string name) =>
            ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(OutputKey, ""), name + ".png"));

        [Serializable]
        private sealed class Report
        {
            public string status;
            public string failure;
            public double seconds;
            public string[] checks;
            public string[] timeline;
            public string[] errors;
            public string[] postconditions;
            public string scope;
        }

        private static void Finish(string failure, bool stopPlay)
        {
            _running = false;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            StopClient();
            Report report = new()
            {
                status = failure == null && Errors.Count == 0 ? "passed" : "failed",
                failure = failure,
                seconds = Elapsed(),
                checks = Checks.ToArray(),
                timeline = Timeline.ToArray(),
                errors = Errors.ToArray(),
                scope = Remote
                    ? "Editor Host + built non-admin client process on loopback. The client's Ready click and chat typing are real X11 input (xdotool); other UI steps are synthetic events. Client screenshots and client.log in this folder. Does not cover remote machines."
                    : "Single-editor Host session; synthetic UI events. Does not cover a remote client process, non-admin permissions or OS input.",
            };
            // Drivers wait for passed/failed; the final status follows the post-Play scene check.
            string result = report.status;
            report.status = "verifying";
            Write(report, OutputDir);
            report.status = result;
            _pending = report;
            if (stopPlay) EditorApplication.isPlaying = false;
        }

        private static void Complete(Report report, string output, string[] problems)
        {
            report.postconditions = problems;
            if (problems.Length > 0)
            {
                report.status = "failed";
                report.failure ??= "Postcondition: " + string.Join("; ", problems);
            }
            Write(report, output);
            Debug.Log($"[SS3D Automation] Lobby lifecycle pilot {report.status}: {output}");
        }

        private static void Write(Report report, string output) =>
            File.WriteAllText(Path.Combine(output, "status.json"), JsonUtility.ToJson(report, true));
    }
}
