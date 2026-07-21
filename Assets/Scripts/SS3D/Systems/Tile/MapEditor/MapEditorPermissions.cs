using FishNet.Connection;
using SS3D.Systems.Tile.MapEditor.Authorizers;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Server-side permission checks for map editor RPCs.
    /// </summary>
    public static class MapEditorPermissions
    {
        private static IMapEditorAuthorizer _authorizer = new AdminMapEditorAuthorizer();

        /// <summary>
        /// Override the default authorizer (e.g. creative-mode Builder role check).
        /// </summary>
        public static void SetAuthorizer(IMapEditorAuthorizer authorizer) =>
            _authorizer = authorizer ?? new AdminMapEditorAuthorizer();

        public static bool TryAuthorize(NetworkConnection conn) => _authorizer.TryAuthorize(conn);
    }
}
