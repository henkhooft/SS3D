using FishNet.Connection;
using SS3D.Core;
using SS3D.Logging;
using SS3D.Permissions;
using SS3D.Systems.PlayerControl;
using System;
using System.Linq;

namespace SS3D.Systems.Tile.MapEditor.Authorizers
{
    /// <summary>
    /// Sandbox servers: every logged-in player may use the map editor, without becoming an admin.
    /// Only players with the Restricted role are refused. Enabled by launching the server with
    /// <see cref="CommandLineFlag"/>; round controls and other admin tools stay admin-only.
    /// </summary>
    public sealed class OpenBuildMapEditorAuthorizer : IMapEditorAuthorizer
    {
        public const string CommandLineFlag = "-openbuild";

        public static bool RequestedOnCommandLine =>
            Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, CommandLineFlag, StringComparison.OrdinalIgnoreCase));

        public bool TryAuthorize(NetworkConnection conn)
        {
            if (conn == null)
                return false;

            string ckey = SubSystems.Get<PlayerSubSystem>().GetCkey(conn);
            if (string.IsNullOrEmpty(ckey))
                return false;

            if (SubSystems.Get<PermissionSubSystem>().TryGetUserRole(ckey, out ServerRoleTypes role)
                && role == ServerRoleTypes.Restricted)
            {
                Log.Warning(typeof(OpenBuildMapEditorAuthorizer), "Restricted user {ckey} denied map edit", Logs.ServerOnly, ckey);
                return false;
            }

            return true;
        }
    }
}
