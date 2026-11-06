using FishNet.Connection;
using FishNet.Object;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// A placed object the map editor's Use tool can open. Implemented by machine panels, which live in
    /// an assembly above this one.
    /// </summary>
    public interface IMapEditorUsable
    {
        NetworkObject NetworkObject { get; }

        /// <summary>Opens for an author the editor has already authorized, with no range or ID check.</summary>
        void ServerOpenForMapEditor(NetworkConnection conn);
    }
}
