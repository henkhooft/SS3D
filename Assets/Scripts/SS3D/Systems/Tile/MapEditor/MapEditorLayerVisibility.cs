using SS3D.Systems.Tile.MapEditor.UI;
using SS3D.Systems.Tile.TileMapCreator;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Maps map editor layer toggles to <see cref="TileLayerCategory"/> visibility groups.
    /// </summary>
    public static class MapEditorLayerVisibility
    {
        public static void Apply(MapEditorViewModel viewModel)
        {
            if (viewModel == null)
                return;

            foreach (TileLayerCategory category in TileLayerCategoryMapping.AllCategories)
                TileLayerVisibilityService.SetGroupVisible(category, viewModel.IsLayerCategoryVisible(category));
        }

        public static void Activate() => TileLayerVisibilityService.Activate();

        public static void Deactivate() => TileLayerVisibilityService.Deactivate();
    }
}
