using FishNet.Connection;
using SS3D.Systems.Tile.MapEditor;

namespace SS3D.Systems.Tile
{
    /// <summary>
    /// Obsolete alias — use <see cref="MapEditorPermissions"/>.
    /// </summary>
    internal static class TileMapEditorPermissions
    {
        internal const Permissions.ServerRoleTypes RequiredRole =
            MapEditor.Authorizers.AdminMapEditorAuthorizer.RequiredRole;

        internal static bool TryAuthorize(NetworkConnection conn) =>
            MapEditorPermissions.TryAuthorize(conn);
    }
}
