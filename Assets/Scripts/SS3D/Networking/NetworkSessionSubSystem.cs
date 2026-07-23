using Coimbra;
using Coimbra.Services.Events;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using SS3D.Application.Events;
using SS3D.Core.Behaviours;
using SS3D.Core.Settings;
using SS3D.Logging;
using SS3D.Networking.Settings;
using System;

namespace SS3D.Networking
{
    /// <summary>
    /// Helps the NetworkManager to understand what we should do in this instance,
    /// if we are a server, or a client, and process respective data.
    /// </summary>
    public sealed class NetworkSessionSubSystem : SubSystem
    {
        public NetworkType NetworkType;

        public string ServerAddress;
        public ushort Port;

        protected override void OnAwake()
        {
            base.OnAwake();

#if UNITY_SERVER
            // Dedicated servers skip the Intro scene (the only place that otherwise calls
            // StartNetworkSession), so start listening for connections here instead.
            AddHandle(ApplicationInitializing.AddListener(HandleApplicationInitializing));
#endif
        }

#if UNITY_SERVER
        private void HandleApplicationInitializing(ref EventContext context, in ApplicationInitializing applicationInitializing)
        {
            StartNetworkSession();
        }
#endif

        /// <summary>
        /// Serilog file-sink name for the current <see cref="NetworkSettings.NetworkType"/> /
        /// ckey. Called from <see cref="CommandLine.CommandLineArgsSubSystem"/> after CLI args
        /// have mutated those settings — registering this on
        /// <see cref="ApplicationPreInitializing"/> raced with CLI processing and could pick
        /// Host's <c>LogHost.json</c> even when <c>-serveronly</c>/<c>-ip=</c> later set
        /// DedicatedServer/Client (the multiplayer harness then waits forever on
        /// <c>LogServer.json</c>/<c>LogClient&lt;ckey&gt;.json</c>).
        /// </summary>
        public static string GetFileLogName(NetworkSettings networkSettings)
        {
            switch (networkSettings.NetworkType)
            {
                case NetworkType.DedicatedServer:
                    return "LogServer.json";
                case NetworkType.Client:
                    return "LogClient" + networkSettings.Ckey + ".json";
                case NetworkType.Host:
                    return "LogHost.json";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Uses the processed args to proceed with game network initialization
        /// </summary>
        public void StartNetworkSession()
        {
            NetworkManager networkManager = InstanceFinder.NetworkManager;
            if (networkManager == null)
            {
                Log.Error(this, "No NetworkManager found; cannot start network session", Logs.Important);
                return;
            }

            ClientConnectionRecovery.EnsureOn(networkManager);

            NetworkSettings networkSettings = ScriptableSettings.GetOrFind<NetworkSettings>();
            NetworkType = networkSettings.NetworkType;
            ServerAddress = networkSettings.ServerAddress;
            Port = Convert.ToUInt16(networkSettings.ServerPort);

            // Re-entry while a prior join is still Starting/Started/Stopping is what storms
            // "Failed to start the client connection" when Boot/Intro reload on disconnect.
            if (!CanStartNetworkSession(networkManager, NetworkType, out string skipReason))
            {
                Log.Warning(this, "Skipping StartNetworkSession: {reason}", Logs.Important, skipReason);
                return;
            }

            Log.Debug(this, "Initializing network session", Logs.Important);

            LocalPlayer.UpdateCkey(networkSettings.Ckey);
            string ckey = networkSettings.Ckey;

            if (networkManager.TryGetComponent(out ClientConnectionRecovery recovery))
            {
                recovery.NotifySessionStartAttempted();
            }

            // Dedicated Server build target defines UNITY_SERVER, which makes FishNet auto-start
            // the transport on Boot (default port). Stop that so Host/Client use NetworkSettings.
            StopAutoStartedConnections(networkManager);

            switch (NetworkType)
            {
                case NetworkType.DedicatedServer:
                    Log.Information(this, "Hosting a new headless server on port {port}", Logs.Important, Port);
                    LogIfConnectionFailedToStart("server", networkManager.ServerManager.StartConnection(Port));
                    break;
                case NetworkType.Client:
                    Log.Information(this, "Joining server {serverAddress}:{port} as {ckey}", Logs.Important, ServerAddress, Port, ckey);
                    LogIfConnectionFailedToStart("client", networkManager.ClientManager.StartConnection(ServerAddress, Port));
                    break;
                case NetworkType.Host:
                    Log.Information(this, "Hosting a new server on port {port}", Logs.Important, Port);
                    LogIfConnectionFailedToStart("server", networkManager.ServerManager.StartConnection(Port));
                    LogIfConnectionFailedToStart("client", networkManager.ClientManager.StartConnection(ServerAddress, Port));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            NetworkSessionStartedEvent networkSessionStartedEvent = new(ckey, NetworkType);
            networkSessionStartedEvent.Invoke(this);
        }

        private static bool CanStartNetworkSession(NetworkManager networkManager, NetworkType networkType, out string skipReason)
        {
            skipReason = null;

            // Host/dedicated may already have a Started server from UNITY_SERVER auto-start;
            // StopAutoStartedConnections clears that before StartConnection. Only gate the
            // client half — stacking StartConnection while Starting/Started/Stopping is what
            // storms "Failed to start the client connection" on Boot/Intro reload.
            if (networkType is not (NetworkType.Client or NetworkType.Host))
            {
                return true;
            }

            LocalConnectionState clientState = networkManager.TransportManager.Transport.GetConnectionState(false);
            if (clientState is LocalConnectionState.Starting
                or LocalConnectionState.Started
                or LocalConnectionState.Stopping)
            {
                skipReason = $"client already {clientState}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Transport.StartConnection() returns false without throwing when it can't start - either the
        /// connection is already Starting/Started (e.g. something else already called StartConnection
        /// on this transport), or the underlying socket bind itself failed (e.g. the OS port is already
        /// bound by another process). This would otherwise fail completely silently, since the "Hosting
        /// a new headless server" log above is written unconditionally before any of this is known.
        /// </summary>
        private void LogIfConnectionFailedToStart(string role, bool started)
        {
            if (!started)
            {
                Log.Error(this, "Failed to start the {role} connection on port {port}. Either it was already starting/started, or the port is already bound by another process.", Logs.Important, role, Port);
            }
        }

        /// <summary>
        /// UNITY_SERVER also makes FishNet auto-start the transport on Boot.unity's own
        /// ServerManager (its own StartOnHeadless option) using the scene-configured default port,
        /// racing with StartNetworkSession here. Stop any such stray connection first so this method
        /// remains the single source of truth for what actually gets started and on what port.
        /// </summary>
        private static void StopAutoStartedConnections(NetworkManager networkManager)
        {
            // Started alone misses Starting — a second StartNetworkSession during Starting
            // skips Stop and StartConnection returns false ("already starting/started").
            LocalConnectionState clientState = networkManager.TransportManager.Transport.GetConnectionState(false);
            if (clientState == LocalConnectionState.Starting || clientState == LocalConnectionState.Started)
            {
                networkManager.ClientManager.StopConnection();
            }

            LocalConnectionState serverState = networkManager.TransportManager.Transport.GetConnectionState(true);
            if (serverState == LocalConnectionState.Starting || serverState == LocalConnectionState.Started)
            {
                Log.Warning(typeof(NetworkSessionSubSystem),
                    "Stopping a server that was already started (often UNITY_SERVER / Dedicated Server build target auto-start). Switch the Editor build target to Standalone for normal Host play.");
                networkManager.ServerManager.StopConnection(true);
            }
        }

        private void OnApplicationQuit()
        {
            CloseNetworkSession();
            InstanceFinder.ServerManager.StopConnection(true);
        }

        /// <summary>
        ///Shuts down client and server to ensure no lingering connections
        /// </summary>
        public void CloseNetworkSession()
        {
            NetworkManager networkManager = InstanceFinder.NetworkManager;
            if (networkManager == null)
            {
                Log.Warning(this, "No NetworkManager found", Logs.Important);
                return;
            }

            Log.Debug(this, "Closing network session", Logs.Important);
            networkManager.TransportManager.Transport.Shutdown();
        }
    }
}