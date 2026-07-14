using SS3D.Data.AssetDatabases;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Runtime catalog built from <see cref="MapEditorCatalogSo"/> and live tile assets.
    /// </summary>
    public sealed class MapEditorCatalog
    {
        private readonly Dictionary<string, MapEditorCatalogEntry> _byAssetName = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<MapEditorCatalogEntry> _entries = new();

        public IReadOnlyList<MapEditorCatalogEntry> Entries => _entries;

        public void Build(IReadOnlyList<GenericObjectSo> assets, MapEditorCatalogSo catalogSo)
        {
            _byAssetName.Clear();
            _entries.Clear();

            HashSet<string> mapped = new(StringComparer.OrdinalIgnoreCase);

            if (catalogSo != null)
            {
                foreach (MapEditorCatalogEntry entry in catalogSo.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.AssetName))
                        continue;

                    _entries.Add(entry);
                    _byAssetName[entry.AssetName] = entry;
                    mapped.Add(entry.AssetName);
                }
            }

            foreach (GenericObjectSo asset in assets)
            {
                if (mapped.Contains(asset.NameString))
                    continue;

                MapEditorCatalogEntry generated = MapEditorCatalogHeuristics.Infer(asset);
                _entries.Add(generated);
                _byAssetName[asset.NameString] = generated;
            }
        }

        public bool TryGetEntry(string assetName, out MapEditorCatalogEntry entry) =>
            _byAssetName.TryGetValue(assetName, out entry);

        public bool TryGetAsset(string assetName, TileResourceLoader loader, out GenericObjectSo asset)
        {
            asset = loader.GetAsset(assetName);
            return asset != null;
        }

        public IEnumerable<MapEditorCatalogEntry> Query(MapEditorMode mode, MapEditorSubcategory subcategory, string search)
        {
            IEnumerable<MapEditorCatalogEntry> query = _entries.Where(e => e.Mode == mode && e.Subcategory == subcategory);

            if (mode == MapEditorMode.Scripting)
                return query;

            if (string.IsNullOrWhiteSpace(search))
                return query.OrderBy(e => e.AssetName);

            string term = search.Trim();
            return query
                .Where(e => MatchesSearch(e, term))
                .OrderBy(e => e.AssetName);
        }

        public static bool IsScriptingSubcategory(MapEditorSubcategory subcategory) =>
            subcategory is MapEditorSubcategory.Atmospherics
                or MapEditorSubcategory.SpawnPlacements
                or MapEditorSubcategory.RandomSpawners
                or MapEditorSubcategory.Triggers;

        public static MapEditorSubcategory[] GetSubcategories(MapEditorMode mode) =>
            mode switch
            {
                MapEditorMode.Upper => new[]
                {
                    MapEditorSubcategory.Flooring, MapEditorSubcategory.Turfs, MapEditorSubcategory.Overlays,
                    MapEditorSubcategory.Walls, MapEditorSubcategory.Doors, MapEditorSubcategory.TileObjects,
                    MapEditorSubcategory.WallAttachments,
                },
                MapEditorMode.Lower => new[]
                {
                    MapEditorSubcategory.Piping, MapEditorSubcategory.Disposals, MapEditorSubcategory.BaseTiles,
                },
                MapEditorMode.Items => new[]
                {
                    MapEditorSubcategory.FoodDrink, MapEditorSubcategory.Tools, MapEditorSubcategory.Medical,
                    MapEditorSubcategory.Security, MapEditorSubcategory.Misc,
                },
                MapEditorMode.Scripting => new[]
                {
                    MapEditorSubcategory.Atmospherics, MapEditorSubcategory.SpawnPlacements,
                    MapEditorSubcategory.RandomSpawners, MapEditorSubcategory.Triggers,
                },
                _ => Array.Empty<MapEditorSubcategory>(),
            };

        public static string GetModeLabel(MapEditorMode mode) =>
            mode switch
            {
                MapEditorMode.Upper => "Upper",
                MapEditorMode.Lower => "Lower",
                MapEditorMode.Items => "Items",
                MapEditorMode.Scripting => "Scripting",
                _ => mode.ToString(),
            };

        public static string GetSubcategoryLabel(MapEditorSubcategory subcategory) =>
            subcategory switch
            {
                MapEditorSubcategory.Flooring => "Flooring",
                MapEditorSubcategory.Turfs => "Turfs",
                MapEditorSubcategory.Overlays => "Overlays",
                MapEditorSubcategory.Walls => "Walls",
                MapEditorSubcategory.Doors => "Doors",
                MapEditorSubcategory.TileObjects => "Tile Objects",
                MapEditorSubcategory.WallAttachments => "Wall Attachments",
                MapEditorSubcategory.Piping => "Piping",
                MapEditorSubcategory.Disposals => "Disposals",
                MapEditorSubcategory.BaseTiles => "Base Tiles",
                MapEditorSubcategory.FoodDrink => "Food & Drink",
                MapEditorSubcategory.Tools => "Tools",
                MapEditorSubcategory.Medical => "Medical",
                MapEditorSubcategory.Security => "Security",
                MapEditorSubcategory.Misc => "Misc",
                MapEditorSubcategory.Atmospherics => "Atmospherics",
                MapEditorSubcategory.SpawnPlacements => "Spawn Placements",
                MapEditorSubcategory.RandomSpawners => "Random Item Spawners",
                MapEditorSubcategory.Triggers => "Triggers",
                MapEditorSubcategory.Uncategorized => "Uncategorized",
                _ => subcategory.ToString(),
            };

        private static bool MatchesSearch(MapEditorCatalogEntry entry, string term)
        {
            if (entry.AssetName.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;

            if (entry.SearchTags == null)
                return false;

            return entry.SearchTags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }
}
