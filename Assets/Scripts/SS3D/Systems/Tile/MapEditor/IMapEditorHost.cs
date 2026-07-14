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
        MapEditorTool CurrentTool { get; }
        bool GridSnapEnabled { get; }
    }
}
