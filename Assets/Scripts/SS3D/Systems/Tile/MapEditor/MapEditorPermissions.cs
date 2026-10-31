using FishNet.Connection;
using SS3D.Core;
using SS3D.Systems.Persistence;
using SS3D.Systems.Rounds;
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

        public static bool TryAuthorize(NetworkConnection conn) =>
            (!SubSystems.TryGet(out RoundSubSystem rounds) || rounds.MapEditingAllowed)
            && (!SubSystems.TryGet(out PersistenceSubSystem persistence) || !persistence.IsRestoringStationTemplate)
            && _authorizer.TryAuthorize(conn);
    }
}
