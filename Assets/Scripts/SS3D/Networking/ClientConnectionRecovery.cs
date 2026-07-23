using Coimbra;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using SS3D.Core;
using SS3D.Core.Settings;
using SS3D.Logging;
using SS3D.Networking.Settings;
using UnityEngine;

namespace SS3D.Networking
{
    /// <summary>
    /// Survives on the DDOL NetworkManager. After the first successful connection, points
    /// FishNet <see cref="DefaultScene"/> offline at Empty so a drop does not reload Boot
    /// (which re-enters Intro and storms <see cref="NetworkSessionSubSystem.StartNetworkSession"/>).
    /// Offers OnGUI Retry/Quit when Boot is gone; Intro failure UI stays on
    /// <see cref="ServerConnectionView"/>.
    /// </summary>
    public sealed class ClientConnectionRecovery : MonoBehaviour
    {
        private const string EmptyOfflineScenePath = "Assets/Content/Scenes/Empty.unity";

        private bool _sessionEverStarted;
        private bool _recoveryVisible;
        private string _status = "Disconnected from server.";

        public static void EnsureOn(NetworkManager networkManager)
        {
            if (networkManager == null)
            {
                return;
            }

            if (networkManager.GetComponent<ClientConnectionRecovery>() != null)
            {
                return;
            }

            networkManager.gameObject.AddComponent<ClientConnectionRecovery>();
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
            _sessionEverStarted = true;
            _recoveryVisible = false;
        }

        private void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                ArmEmptyOfflineScene();
            }
        }

        private void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                ArmEmptyOfflineScene();
                _recoveryVisible = false;
                return;
            }

            if (args.ConnectionState != LocalConnectionState.Stopped || !_sessionEverStarted)
            {
                return;
            }

            NetworkSettings settings = ScriptableSettings.GetOrFind<NetworkSettings>();
            if (settings.NetworkType == NetworkType.DedicatedServer)
            {
                return;
            }

            // Intro still loaded: ServerConnectionView owns retry. After offline Empty
            // unload Boot, NetworkSessionSubSystem is gone and this OnGUI is the fallback.
            if (SubSystems.TryGet(out NetworkSessionSubSystem _))
            {
                return;
            }

            _status = "Could not connect, or connection to the server was lost.";
            _recoveryVisible = true;
            Log.Information(this, "{status}", Logs.Important, _status);
        }

        private void ArmEmptyOfflineScene()
        {
            DefaultScene defaultScene = GetComponent<DefaultScene>();
            if (defaultScene == null)
            {
                return;
            }

            if (defaultScene.GetOfflineScene() == EmptyOfflineScenePath)
            {
                return;
            }

            defaultScene.SetOfflineScene(EmptyOfflineScenePath);
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

            LocalConnectionState state = networkManager.TransportManager.Transport.GetConnectionState(false);
            if (state == LocalConnectionState.Starting
                || state == LocalConnectionState.Started
                || state == LocalConnectionState.Stopping)
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
                }

                return;
            }

            NotifySessionStartAttempted();
            session.StartNetworkSession();
        }
    }
}
