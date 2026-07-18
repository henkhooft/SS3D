using UnityEngine;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Context exposed to placement/preview helpers while the map editor is active.
    /// </summary>
    public interface IMapEditorHost
    {
        bool IsActive { get; }
        bool MouseOverUI { get; }
        bool IsDeleting { get; }
        bool IsOrbiting { get; }
        MapEditorTool CurrentTool { get; }
        bool GridSnapEnabled { get; }

        /// <summary>Camera used for placement picks while the editor is open.</summary>
        Camera PickCamera { get; }
    }
}
