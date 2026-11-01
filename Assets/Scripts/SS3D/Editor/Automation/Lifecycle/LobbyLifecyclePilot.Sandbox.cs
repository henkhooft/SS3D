using System.Linq;
using FishNet;
using SS3D.Systems.Entities;
using SS3D.Systems.Entities.Humanoid;
using UnityEngine;
using UnityEngine.InputSystem;
using SS3D.Core;
using SS3D.Systems.Comms;
using SS3D.Systems.Rounds;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.MapEditor;
using SS3D.Systems.Tile.MapEditor.Commands;
using UnityEditor;
using UnityEngine.UIElements;

namespace SS3D.Editor.Automation
{
    /// <summary>
    /// Sandbox server pilot (<c>2026-11_sandbox-server.md</c>): the host applies the sandbox profile in
    /// Play Mode, as <c>-sandbox</c> does at boot, then lets the server run two rounds on its own with
    /// nobody pressing Ready or Start.
    /// </summary>
    public static partial class LobbyLifecyclePilot
    {
        private const string SandboxMode = "sandbox";

        private const string SandboxScope = "Single-editor Host session with the sandbox profile applied in Play Mode "
            + "(no -sandbox flag in the editor). Two automatic round cycles; nobody presses Ready or Start. "
            + "Does not cover a remote client, non-admin sandbox players or a dedicated server build.";

        [MenuItem("SS3D/Testing/Run Sandbox Pilot")]
        public static void RunSandbox() => Start(SandboxMode);

        private static bool AutosaveExists() =>
            SubSystems.Get<SS3D.Systems.Persistence.PersistenceSubSystem>().StationTemplateExists(_validationStation + RoundSubSystem.AutosaveSuffix);

        private static bool StationEmpty() => SubSystems.Get<TileSubSystem>().CurrentMap.ChunkCount == 0;

        /// <summary>Places an Assistant spawn on a plenum tile the template left empty, so its presence proves the edit.</summary>
        private static void EditEmptySpawnTile()
        {
            TileSubSystem tiles = SubSystems.Get<TileSubSystem>();
            _mapEditSpawnPosition = tiles.CurrentMap.GetAllChunks()
                .SelectMany(chunk => chunk.GetAllTilePlacedObjects())
                .Where(tile => tile.Layer == TileLayer.Plenum)
                .Select(tile => tile.transform.position)
                .First(position => !tiles.SpawnPoints.HasAt(position));
            SubSystems.Get<MapEditorSubSystem>().SubmitCommands(new MapEditorCommandDto[] { new MapEditorCommandDto
            {
                Kind = MapEditorCommandKind.PlaceSpawnPoint, AssetName = "spawn:job:Assistant",
                Position = _mapEditSpawnPosition, Direction = Direction.North,
            } });
        }

        private static Human _corpse;
        private static int _resetHealthLines;
        private static int _bugLines;

        /// <summary>Lines of a jsonl file under the server's Data folder (LocalStorage layout).</summary>
        private static string[] JsonlLines(string name)
        {
            string path = SS3D.Data.Paths.GetPath(SS3D.Data.GamePaths.Data, true) + name + ".jsonl";
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllLines(path) : System.Array.Empty<string>();
        }

        private static T LastJsonl<T>(string name) => JsonUtility.FromJson<T>(JsonlLines(name).Last());

        private static RespawnPrompt Prompt() => SubSystems.Get<EntitySubSystem>().GetComponent<RespawnPrompt>();

        private static bool LocalEntityIsGhost() =>
            SubSystems.Get<EntitySubSystem>().GetSpawnedEntity(Local()) is { } entity
            && entity.GetComponent<HumanoidGhostController>() != null;

        private static Vector3 Flat(Vector3 position) => new(position.x, 0, position.z);

        private static ResetHealthEntry _firstReset;

        /// <summary>The reset-health line lands just after Stopped; wait for this reset's line.</summary>
        private static bool ResetLogged() =>
            JsonlLines(SS3D.Data.Persistence.PersistencePaths.ResetHealth).Length > _resetHealthLines + (_firstReset != null ? 1 : 0);

        /// <summary>The reset wrote its health line; nothing from the station is left; the second reset matches the first.</summary>
        private static void VerifyResetHealth(int resets)
        {
            Require(JsonlLines(SS3D.Data.Persistence.PersistencePaths.ResetHealth).Length == _resetHealthLines + resets,
                $"reset-health.jsonl has a line for reset {resets}");
            ResetHealthEntry entry = LastJsonl<ResetHealthEntry>(SS3D.Data.Persistence.PersistencePaths.ResetHealth);
            Timeline.Add($"reset {resets}: {JsonUtility.ToJson(entry)}");
            Require(entry.roundId == Rounds().RoundId && entry.players == 1, "Health line carries round ID and player count");
            Require(entry.tileObjects == 0 && entry.items == 0, "No tile objects or items survive the unload");
            if (resets == 1)
            {
                _firstReset = entry;
                return;
            }

            Require(entry.networkObjects == _firstReset.networkObjects,
                $"Spawned network objects match the first reset ({_firstReset.networkObjects} → {entry.networkObjects})");
        }

        private static string SidebarSub() => Shell()?.Q<Label>(className: "lobby-shell__countdown-sub")?.text;

        private static Step[] BuildSandboxSteps() => new Step[]
        {
            new Step
            {
                Name = "Lobby ready on host boot",
                Timeout = 180,
                Until = () => Online() && Local() != null && Rounds() != null && Visible(Shell())
                    && Rounds().CurrentRoundState == RoundState.Stopped,
                Verify = () =>
                {
                    Require(!Rounds().AutoStartEnabled && Rounds().AutoStartSecondsLeft == 0, "A normal server does not start rounds on its own");
                    CreateValidationStation(valid: true);
                    _resetHealthLines = JsonlLines(SS3D.Data.Persistence.PersistencePaths.ResetHealth).Length;
                    _bugLines = JsonlLines(SS3D.Data.Persistence.PersistencePaths.BugReports).Length;
                },
            },
            new Step
            {
                Name = "Apply the sandbox profile with a two-minute first round",
                Enter = () =>
                {
                    ServerSettings settings = SandboxProfile.Apply(Rounds().SettingsForServer);
                    settings.Map = _validationStation;
                    settings.RoundLengthMinutes = 2;
                    settings.RespawnCooldownSeconds = 8;
                    Rounds().ApplyServerSettingsForTesting(settings);
                },
                Until = () => Rounds().AutoStartSecondsLeft > 0 && SidebarSub() == "Starting In",
                Verify = () =>
                {
                    Require(!IsReady(), "Countdown started without anyone pressing Ready");
                    Require(!MapEditorPermissions.PersistenceAllowed, "Sandbox profile turns map persistence off");
                    Require(Rounds().Motd == SandboxProfile.Motd, "MOTD carries the sandbox rules");
                    Capture("sandbox-autostart-countdown");
                },
            },
            new Step
            {
                Name = "First round starts and spawns the unready host",
                Timeout = 180,
                Until = () => Rounds().CurrentRoundState == RoundState.Ongoing && Spawned(),
                Verify = () =>
                {
                    // Let the body finish starting on the client before it dies.
                    Settle(2);
                    Require(SubSystems.Get<SS3D.Systems.Gamemodes.GamemodeSubSystem>().CurrentGamemodeName == "Extended", "Sandbox rounds run Extended");
                    Require(Rounds().StationName == _validationStation, "Auto-start restored the selected station");
                },
            },
            new Step
            {
                Name = "Body finishes starting",
                Until = Settled,
            },
            new Step
            {
                Name = "Host dies and becomes a ghost with a respawn countdown",
                Enter = () =>
                {
                    _corpse = (Human)SubSystems.Get<EntitySubSystem>().GetSpawnedEntity(Local());
                    _corpse.Kill();
                },
                Until = () => LocalEntityIsGhost() && Prompt().IsShown,
                Verify = () =>
                {
                    Require(!Prompt().CanRespawn, "Respawn button waits for the cooldown");
                    Require(!SubSystems.Get<EntitySubSystem>().TryRespawn(InstanceFinder.ClientManager.Connection, out string refusal)
                        && refusal.StartsWith("You can respawn in"), "Server refuses a respawn during the cooldown");
                    Capture("sandbox-ghost-respawn-prompt");
                },
            },
            new Step
            {
                Name = "Ghost opens the map editor",
                Enter = () => TapKey(Key.B),
                Until = MapEditorActive,
            },
            new Step
            {
                Name = "Ghost edits the map",
                Enter = EditEmptySpawnTile,
                Until = () => UnsavedMapEdits() && MapEditSpawnPresent(),
                Verify = () => Capture("sandbox-ghost-editing"),
            },
            new Step
            {
                Name = "Ghost closes the map editor",
                Enter = () => TapKey(Key.B),
                Until = () => !MapEditorActive() && Settled(),
                Verify = () => Require(LocalEntityIsGhost(), "Still a ghost after closing the editor"),
            },
            new Step
            {
                Name = "Respawn button enables after the cooldown",
                Timeout = 20,
                Until = () => Prompt().CanRespawn,
            },
            new Step
            {
                Name = "Respawn gives a fresh body at a spawn point; the corpse stays",
                Enter = () => Click(Root().Q<Button>("respawn-prompt-button")),
                Until = () => Spawned() && !LocalEntityIsGhost() && !Prompt().IsShown,
                Verify = () =>
                {
                    Entity body = SubSystems.Get<EntitySubSystem>().GetSpawnedEntity(Local());
                    Require(body is Human && body != _corpse, "Respawned into a new Human");
                    Require(_corpse != null && _corpse.gameObject.activeInHierarchy, "The corpse stays where it fell");
                    Vector3 spawn = SubSystems.Get<TileSubSystem>().SpawnPoints.Records[0].Position;
                    Require(Vector3.Distance(Flat(body.transform.position), Flat(spawn)) < 1.5f, "New body stands at a spawn point");
                    Capture("sandbox-respawned");
                },
            },
            new Step
            {
                Name = "/bug in chat saves a report with round and state",
                Enter = () => SubSystems.Get<CommsSubSystem>().SendOoc(OocChannel.Ooc, "/bug sandbox pilot report"),
                Until = () => HostChatContains("Bug report saved. Thanks!"),
                Verify = () =>
                {
                    Require(JsonlLines(SS3D.Data.Persistence.PersistencePaths.BugReports).Length == _bugLines + 1, "bugs.jsonl gained one line");
                    BugReportEntry bug = LastJsonl<BugReportEntry>(SS3D.Data.Persistence.PersistencePaths.BugReports);
                    Require(bug.note == "sandbox pilot report" && bug.state == "alive" && bug.roundId == Rounds().RoundId
                        && bug.ckey == Local().Ckey, "Report carries note, alive state, round ID and ckey");
                    Require(!HostChatContains("/bug sandbox pilot report"), "The command is not echoed to OOC chat");
                },
            },
            new Step
            {
                Name = "One-minute warning",
                Timeout = 90,
                Until = () => HostChatContains("The round ends in 1 minute."),
                Verify = () => Require(Rounds().CurrentRoundState == RoundState.Ongoing, "Warning arrives while the round runs"),
            },
            new Step
            {
                Name = "First round ends on its own and the station is wiped",
                Timeout = 90,
                Until = () => Rounds().CurrentRoundState == RoundState.Stopped && !Spawned() && StationEmpty() && ResetLogged(),
                Verify = () =>
                {
                    Require(!AutosaveExists(), "No round-end autosave with persistence off");
                    Require(UnityEngine.Object.FindObjectsByType<Human>(FindObjectsSortMode.None).Length == 0,
                        "No bodies or corpses survive the reset");
                    VerifyResetHealth(1);
                    ServerSettings settings = Rounds().SettingsForServer;
                    settings.RoundLengthMinutes = 1;
                    Rounds().ApplyServerSettingsForTesting(settings);
                },
            },
            new Step
            {
                Name = "Second countdown starts without Ready",
                Timeout = 30,
                Until = () => Rounds().AutoStartSecondsLeft > 0,
                Verify = () => Require(!IsReady(), "Ready flags are not remembered across rounds"),
            },
            new Step
            {
                Name = "Second round starts on a fresh station",
                Timeout = 180,
                Until = () => Rounds().CurrentRoundState == RoundState.Ongoing && Spawned(),
                Verify = () =>
                {
                    Require(!MapEditSpawnPresent(), "Last round's edit is gone from the fresh station");
                    Require(!UnsavedMapEdits(), "Editor history starts clean");
                },
            },
            new Step
            {
                Name = "Second round ends on its own",
                Timeout = 120,
                Until = () => Rounds().CurrentRoundState == RoundState.Stopped && !Spawned() && StationEmpty() && ResetLogged(),
                Verify = () =>
                {
                    VerifyResetHealth(2);
                    Capture("sandbox-second-reset");
                },
            },
        };
    }
}
