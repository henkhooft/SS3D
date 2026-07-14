using SS3D.Systems.Tile.TileMapCreator;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Maps map editor layer toggles to <see cref="TileLayerCategory"/> visibility groups.
    /// </summary>
    public static class MapEditorLayerVisibility
    {
        public static void Apply(bool showUpper, bool showLower, bool showPiping)
        {
            TileLayerVisibilityService.SetGroupVisible(TileLayerCategory.Turfs, showUpper);
            TileLayerVisibilityService.SetGroupVisible(TileLayerCategory.Furniture, showUpper);
            TileLayerVisibilityService.SetGroupVisible(TileLayerCategory.WallMounts, showUpper);
            TileLayerVisibilityService.SetGroupVisible(TileLayerCategory.Overlays, showUpper);
            TileLayerVisibilityService.SetGroupVisible(TileLayerCategory.Items, showUpper);

            TileLayerVisibilityService.SetGroupVisible(TileLayerCategory.Plenums, showLower);
            TileLayerVisibilityService.SetGroupVisible(TileLayerCategory.WiresAndPipes, showLower || showPiping);
        }

        public static void Activate() => TileLayerVisibilityService.Activate();

        public static void Deactivate() => TileLayerVisibilityService.Deactivate();
    }
}
