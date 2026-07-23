using Coimbra;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using SS3D.Core;
using SS3D.Core.Settings;
using SS3D.Data.Generated;
using SS3D.Logging;
using SS3D.Networking.Settings;
using UnityEngine;

namespace SS3D.Networking
{
    /// <summary>
    /// Session lifecycle owner (DDOL on NetworkManager). Tracks <see cref="SessionState"/>,
    /// arms Empty as FishNet offline after the first successful connect, and offers OnGUI
    /// Retry/Quit when Boot is gone. Intro failure UI stays on <see cref="ServerConnectionView"/>.
    /// </summary>
    public sealed class ClientConnectionRecovery : MonoBehaviour
    {
        private bool _reachedOnline;
        private bool _recoveryVisible;
        private string _status = "Disconnected from server.";

        public SessionState State { get; private set; } = SessionState.Cold;

        public static ClientConnectionRecovery Instance { get; private set; }

        public static void EnsureOn(NetworkManager networkManager)
        {
            if (networkManager == null)
            {
                return;
            }

            ClientConnectionRecovery existing = networkManager.GetComponent<ClientConnectionRecovery>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            Instance = networkManager.gameObject.AddComponent<ClientConnectionRecovery>();
        }

        /// <summary>
        /// Whether a new join/host attempt may call StartConnection.
        /// </summary>
        public bool CanStart
        {
            get
            {
                if (State is SessionState.Connecting or SessionState.Online or SessionState.Disconnecting)
                {
                    return false;
                }

                NetworkManager networkManager = InstanceFinder.NetworkManager;
                if (networkManager == null)
                {
                    return false;
                }

                NetworkSettings settings = ScriptableSettings.GetOrFind<NetworkSettings>();
                if (settings.NetworkType is not (NetworkType.Client or NetworkType.Host))
                {
                    return true;
                }

                LocalConnectionState clientState = networkManager.TransportManager.Transport.GetConnectionState(false);
                return clientState is not (LocalConnectionState.Starting
                    or LocalConnectionState.Started
                    or LocalConnectionState.Stopping);
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (InstanceFinder.ClientManager != null)
            {
                InstanceFinder.ClientManager.OnClientConnectionState += HandleClientConnectionState;
            }

            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnServerConnectionState += HandleServerConnectionState;
            }
        }

        private void OnDisable()
        {
            if (InstanceFinder.ClientManager != null)
            {
                InstanceFinder.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
            }

            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
            }
        }

        /// <summary>
        /// Called from <see cref="NetworkSessionSubSystem"/> when a join/host attempt begins.
        /// </summary>
        public void NotifySessionStartAttempted()
        {
            SetState(SessionState.Connecting);
            _recoveryVisible = false;
        }

        /// <summary>
        /// Called when <c>StartConnection</c> returns false before any transport Started event.
        /// </summary>
        public void NotifySessionStartFailed()
        {
            if (State == SessionState.Connecting)
            {
                SetState(SessionState.WaitingForServer);
            }
        }

        private void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    EnterOnline();
                    break;
                case LocalConnectionState.Stopping when State == SessionState.Online:
                    SetState(SessionState.Disconnecting);
                    break;
                case LocalConnectionState.Stopped when State is SessionState.Online or SessionState.Disconnecting or SessionState.Connecting:
                    // Host/dedicated: server stop alone does not drive client WaitingForServer UI.
                    if (!_reachedOnline)
                    {
                        SetState(SessionState.WaitingForServer);
                    }

                    break;
            }
        }

        private void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    EnterOnline();
                    _recoveryVisible = false;
                    return;

                case LocalConnectionState.Stopping when State == SessionState.Online:
                    SetState(SessionState.Disconnecting);
                    return;

                case LocalConnectionState.Stopped:
                    if (State is not (SessionState.Connecting or SessionState.Online or SessionState.Disconnecting))
                    {
                        return;
                    }

                    EnterWaitingForServer();
                    return;
            }
        }

        private void EnterWaitingForServer()
        {
            SetState(SessionState.WaitingForServer);
            SubSystems.SetSuppressMissingErrors(true);

            NetworkSettings settings = ScriptableSettings.GetOrFind<NetworkSettings>();
            if (settings.NetworkType == NetworkType.DedicatedServer)
            {
                return;
            }

            // Intro/Boot still loaded: ServerConnectionView owns retry. After Empty offline,
            // those scenes are gone — show OnGUI even though NetworkSession is DDOL now.
            if (IsConnectionUiSceneLoaded())
            {
                return;
            }

            _status = "Could not connect, or connection to the server was lost.";
            _recoveryVisible = true;
            Log.Information(this, "{status}", Logs.Important, _status);
        }

        private static bool IsConnectionUiSceneLoaded()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                string name = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name;
                if (name == Scenes.Intro || name == Scenes.Boot || name == Scenes.Launcher)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnterOnline()
        {
            _reachedOnline = true;
            SubSystems.SetSuppressMissingErrors(false);
            ArmEmptyOfflineScene();
            SetState(SessionState.Online);
            if (InstanceFinder.IsServer)
            {
                NetworkSystemsHub.EnsureSpawned();
            }
        }

        private void SetState(SessionState next)
        {
            if (State == next)
            {
                return;
            }

            Log.Debug(this, "SessionState {from} → {to}", Logs.Important, State, next);
            State = next;

            // Hub NetworkObjects despawn while Stopping/Disconnecting, before WaitingForServer.
            // Missing Get lookups from device OnDestroy are expected then — same class as teardown.
            if (next == SessionState.Disconnecting)
            {
                SubSystems.SetSuppressMissingErrors(true);
            }
        }

        private void ArmEmptyOfflineScene()
        {
            DefaultScene defaultScene = GetComponent<DefaultScene>();
            if (defaultScene == null)
            {
                return;
            }

            if (defaultScene.GetOfflineScene() == Scenes.EmptyPath)
            {
                return;
            }

            defaultScene.SetOfflineScene(Scenes.EmptyPath);
            Log.Debug(this, "Armed Empty offline scene for disconnect (avoid Boot/Intro reload storm)", Logs.Important);
        }

        private void OnGUI()
        {
            if (!_recoveryVisible)
            {
                return;
            }

            const float width = 420f;
            const float height = 140f;
            Rect area = new(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(_status);
            GUILayout.Space(12f);

            if (GUILayout.Button("Retry connection", GUILayout.Height(32f)))
            {
                Retry();
            }

            if (GUILayout.Button("Quit", GUILayout.Height(28f)))
            {
                UnityEngine.Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }

            GUILayout.EndArea();
        }

        private void Retry()
        {
            NetworkManager networkManager = InstanceFinder.NetworkManager;
            if (networkManager == null)
            {
                return;
            }

            if (!CanStart)
            {
                _status = "Connection still settling — wait a moment, then retry.";
                return;
            }

            _recoveryVisible = false;

            if (!SubSystems.TryGet(out NetworkSessionSubSystem session))
            {
                // Boot was unloaded (offline Empty). Start from NetworkSettings directly.
                NetworkSettings settings = ScriptableSettings.GetOrFind<NetworkSettings>();
                LocalPlayer.UpdateCkey(settings.Ckey);
                NotifySessionStartAttempted();
                bool started = networkManager.ClientManager.StartConnection(
                    settings.ServerAddress,
                    settings.ServerPort);
                if (!started)
                {
                    _status = "Failed to start client connection.";
                    _recoveryVisible = true;
                    SetState(SessionState.WaitingForServer);
                }

                return;
            }

            session.StartNetworkSession();
        }
    }
}
