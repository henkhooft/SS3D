using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using SS3D.Systems.Electricity;
using SS3D.Systems.Entities;
using UnityEngine;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Builder-facing map editor settings that outlive the editor overlay, so a map can be play-tested
    /// right after closing it: always-powered light fixtures (server-wide) and per-builder invincibility.
    /// </summary>
    public sealed partial class MapEditorSubSystem
    {
        private bool _invincibleSentForSession;

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            LightingDevBypass.MapEditorOverride = false;
        }

        private static void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            // FishNet reuses client ids; never let a new joiner inherit a builder's invincibility.
            if (args.ConnectionState == RemoteConnectionState.Stopped)
                EntityInvincibility.ClearServer(conn.ClientId);
        }

        /// <summary>Invincible defaults on; push it the first time the editor opens this session.</summary>
        private void ApplySessionSettingsOnOpen()
        {
            if (_invincibleSentForSession)
                return;

            _invincibleSentForSession = true;
            RpcSetInvincible(_viewModel.Invincible, LocalConnection);
        }

        private void OnAlwaysPowerLightsChanged(bool value)
        {
            _viewModel.AlwaysPowerLights = value;
            RpcSetAlwaysPowerLights(value, LocalConnection);
        }

        private void OnInvincibleChanged(bool value)
        {
            _viewModel.Invincible = value;
            RpcSetInvincible(value, LocalConnection);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcSetAlwaysPowerLights(bool enabled, NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            LightingDevBypass.MapEditorOverride = enabled;
            foreach (LightPower fixture in FindObjectsByType<LightPower>(FindObjectsSortMode.None))
                fixture.RefreshVisuals();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcSetInvincible(bool enabled, NetworkConnection conn = null)
        {
            if (conn == null || !MapEditorPermissions.TryAuthorize(conn))
                return;

            EntityInvincibility.SetServer(conn.ClientId, enabled);
            TargetSetInvincible(conn, enabled);
        }

        [TargetRpc]
        private void TargetSetInvincible(NetworkConnection conn, bool enabled) =>
            EntityInvincibility.LocalPlayerInvincible = enabled;
    }
}
