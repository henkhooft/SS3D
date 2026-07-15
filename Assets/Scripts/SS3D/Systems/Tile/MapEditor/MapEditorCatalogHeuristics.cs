using SS3D.Data.AssetDatabases;
using SS3D.Systems.Tile.Connections;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Heuristic mapping from tile assets to library taxonomy when not explicitly cataloged.
    /// </summary>
    public static class MapEditorCatalogHeuristics
    {
        public static MapEditorCatalogEntry Infer(GenericObjectSo asset)
        {
            if (asset is ItemObjectSo)
            {
                return new MapEditorCatalogEntry
                {
                    AssetName = asset.NameString,
                    Mode = MapEditorMode.Items,
                    Subcategory = MapEditorSubcategory.Misc,
                    SearchTags = new[] { "item" },
                };
            }

            if (asset is not TileObjectSo tile)
            {
                return Uncategorized(asset.NameString);
            }

            return tile.layer switch
            {
                TileLayer.Turf when tile.genericType == TileObjectGenericType.Wall =>
                    Entry(asset.NameString, MapEditorMode.Upper, MapEditorSubcategory.Walls, "wall"),
                TileLayer.Turf when tile.genericType == TileObjectGenericType.Door =>
                    Entry(asset.NameString, MapEditorMode.Upper, MapEditorSubcategory.Doors, "door"),
                TileLayer.Turf when tile.genericType == TileObjectGenericType.Floor =>
                    Entry(asset.NameString, MapEditorMode.Upper, MapEditorSubcategory.Flooring, "floor", "plating"),
                TileLayer.Turf =>
                    Entry(asset.NameString, MapEditorMode.Upper, MapEditorSubcategory.Turfs, "turf"),
                TileLayer.Plenum when tile.genericType == TileObjectGenericType.Plenum =>
                    Entry(asset.NameString, MapEditorMode.Lower, MapEditorSubcategory.BaseTiles, "plenum"),
                TileLayer.Plenum =>
                    Entry(asset.NameString, MapEditorMode.Lower, MapEditorSubcategory.BaseTiles, "lattice", "catwalk", "base"),
                TileLayer.FurnitureBase or TileLayer.FurnitureTop =>
                    Entry(asset.NameString, MapEditorMode.Upper, MapEditorSubcategory.TileObjects, "furniture"),
                TileLayer.WallMountLow or TileLayer.WallMountHigh =>
                    Entry(asset.NameString, MapEditorMode.Upper, MapEditorSubcategory.WallAttachments, "wall mount"),
                TileLayer.Disposal =>
                    Entry(asset.NameString, MapEditorMode.Lower, MapEditorSubcategory.Disposals, "disposal"),
                TileLayer.Wire =>
                    Entry(asset.NameString, MapEditorMode.Lower, MapEditorSubcategory.Piping, "wire", "cable"),
                TileLayer.PipeLeft or TileLayer.PipeRight or TileLayer.PipeSurface or TileLayer.PipeMiddle
                    when tile.genericType == TileObjectGenericType.Pipe =>
                    Entry(asset.NameString, MapEditorMode.Lower, MapEditorSubcategory.Piping, "pipe"),
                _ => Uncategorized(asset.NameString),
            };
        }

        public static MapEditorCatalogEntry CreateEraserEntry() =>
            new()
            {
                AssetName = "__eraser__",
                Mode = MapEditorMode.Upper,
                Subcategory = MapEditorSubcategory.Walls,
                SearchTags = new[] { "eraser", "delete", "clear" },
                IsEraser = true,
            };

        private static MapEditorCatalogEntry Entry(string name, MapEditorMode mode, MapEditorSubcategory sub, params string[] tags) =>
            new()
            {
                AssetName = name,
                Mode = mode,
                Subcategory = sub,
                SearchTags = tags,
            };

        private static MapEditorCatalogEntry Uncategorized(string name) =>
            Entry(name, MapEditorMode.Upper, MapEditorSubcategory.Uncategorized, "uncategorized");
    }
}
