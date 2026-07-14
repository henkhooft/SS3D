namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Top-level object library mode matching the map editor UI mockup.
    /// </summary>
    public enum MapEditorMode
    {
        Upper,
        Lower,
        Items,
        Scripting,
    }

    /// <summary>
    /// Subcategory within a map editor mode.
    /// </summary>
    public enum MapEditorSubcategory
    {
        // Upper
        Flooring,
        Turfs,
        Walls,
        Doors,
        TileObjects,
        WallAttachments,

        // Lower
        Piping,
        Disposals,
        BaseTiles,

        // Items
        FoodDrink,
        Tools,
        Medical,
        Security,
        Misc,

        // Scripting (UI stub in v1)
        Atmospherics,
        SpawnPlacements,
        RandomSpawners,
        Triggers,

        Uncategorized,
    }
}
