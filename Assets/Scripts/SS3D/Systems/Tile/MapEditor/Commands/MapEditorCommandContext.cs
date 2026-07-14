using SS3D.Data.AssetDatabases;

namespace SS3D.Systems.Tile.MapEditor.Commands
{
    public sealed class MapEditorCommandContext
    {
        public MapEditorCommandContext(TileMap map, TileResourceLoader loader, ConstructionService construction)
        {
            Map = map;
            Loader = loader;
            Construction = construction;
        }

        public TileMap Map { get; }
        public TileResourceLoader Loader { get; }
        public ConstructionService Construction { get; }
        public MapEditorPlacementMode PlacementMode { get; set; } = MapEditorPlacementMode.Normal;

        public GenericObjectSo ResolveAsset(string assetName) =>
            Loader != null ? Loader.GetAsset(assetName) : null;
    }
}
