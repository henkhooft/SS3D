namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Active map editor tool. Area is reserved for creative-mode area authoring (Phase 2).
    /// Move is retained for the underlying drag-relocate RPC path even though the current
    /// toolbar UI no longer exposes a dedicated button for it.
    /// </summary>
    public enum MapEditorTool
    {
        Select,
        Edit,
        Move,
        Area,
        Dropper,
        Delete,
    }
}
