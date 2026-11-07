using SS3D.Data.AssetDatabases;
using SS3D.Systems.Tile.FloorVisuals;
using SS3D.Systems.Tile.GasMarkers;
using SS3D.Systems.Tile.SpawnPoints;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Stable catalog keys for <see cref="FloorDecalDefinition"/> entries in the object library.
    /// </summary>
    public static class MapEditorFloorDecalCatalog
    {
        public const string Prefix = "floor-decal:";

        public static string Encode(ushort id) => $"{Prefix}{id}";

        public static bool TryDecode(string assetName, out ushort id)
        {
            id = 0;
            if (string.IsNullOrEmpty(assetName) || !assetName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            return ushort.TryParse(assetName.AsSpan(Prefix.Length), out id);
        }
    }

    /// <summary>
    /// Runtime catalog built from <see cref="MapEditorCatalogSo"/> and live tile assets.
    /// </summary>
    public sealed class MapEditorCatalog
    {
        private readonly Dictionary<string, MapEditorCatalogEntry> _byAssetName = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<MapEditorCatalogEntry> _entries = new();

        public IReadOnlyList<MapEditorCatalogEntry> Entries => _entries;

        /// <summary>Leaves <see cref="MapEditorSandboxRules"/> denied entries out of <see cref="Query"/> (sandbox players).</summary>
        public bool HideSandboxDenied { get; set; }

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
                if (generated == null)
                    continue;

                _entries.Add(generated);
                _byAssetName[asset.NameString] = generated;
            }

            AppendFloorDecals(mapped);
            AppendSpawnPoints(mapped);
            AppendGasMarkers();
        }

        private void AppendFloorDecals(HashSet<string> mapped)
        {
            FloorDecalCatalog catalog = FloorDecalCatalog.Get();
            if (catalog?.Definitions == null)
                return;

            foreach (FloorDecalDefinition definition in catalog.Definitions)
            {
                if (definition == null || definition.Id == 0)
                    continue;

                string assetName = MapEditorFloorDecalCatalog.Encode(definition.Id);
                if (mapped.Contains(assetName))
                    continue;

                MapEditorCatalogEntry entry = Virtual(assetName, MapEditorSubcategory.Decals,
                    "decal", "overlay", definition.DisplayName);
                _entries.Add(entry);
                _byAssetName[assetName] = entry;
            }
        }

        private void AppendGasMarkers()
        {
            foreach (GasMarkerKind kind in Enum.GetValues(typeof(GasMarkerKind)))
            {
                MapEditorCatalogEntry entry = Virtual(MapEditorGasMarkerCatalog.Encode(kind), MapEditorSubcategory.StartingAir,
                    "air", "gas", "atmos", MapEditorGasMarkerCatalog.DisplayName(kind));
                _entries.Add(entry);
                _byAssetName[entry.AssetName] = entry;
            }
        }

        private void AppendSpawnPoints(HashSet<string> mapped)
        {
            foreach (string jobName in MapEditorSpawnCatalog.DefaultJobNames)
            {
                string assetName = MapEditorSpawnCatalog.EncodeJob(jobName);
                if (mapped.Contains(assetName))
                    continue;

                MapEditorCatalogEntry entry = Virtual(assetName, MapEditorSubcategory.SpawnPoints, "spawn", "job", jobName);
                _entries.Add(entry);
                _byAssetName[assetName] = entry;
                mapped.Add(assetName);
            }

            foreach (AntagonistSpawnCategory category in Enum.GetValues(typeof(AntagonistSpawnCategory)))
            {
                string assetName = MapEditorSpawnCatalog.EncodeAntagonist(category);
                if (mapped.Contains(assetName))
                    continue;

                MapEditorCatalogEntry entry = Virtual(assetName, MapEditorSubcategory.SpawnPoints,
                    "spawn", "antag", "antagonist", MapEditorSpawnCatalog.FormatAntagonist(category));
                _entries.Add(entry);
                _byAssetName[assetName] = entry;
                mapped.Add(assetName);
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
            IEnumerable<MapEditorCatalogEntry> query = _entries.Where(e => e.Mode == mode && e.Subcategory == subcategory
                && !(HideSandboxDenied && MapEditorSandboxRules.IsDeniedAsset(e.AssetName)));

            // Marker rails other than Spawn Points list in kind order and ignore search (palette-wide search is S2).
            if (MapEditorSubTabs.TryGet(subcategory, out MapEditorSubTab row) && row.IsMarker
                && subcategory != MapEditorSubcategory.SpawnPoints)
                return query;

            if (string.IsNullOrWhiteSpace(search))
                return query.OrderBy(e => e.AssetName);

            string term = search.Trim();
            return query
                .Where(e => MatchesSearch(e, term))
                .OrderBy(e => e.AssetName);
        }

        /// <summary>
        /// Whether the palette draws this sub-tab: sandbox players never see <see cref="MapEditorSubTab.SandboxRestricted"/>
        /// rows, and a sub-tab with nothing in it is not shown. Rooms lists areas, not catalog entries, so it always shows.
        /// </summary>
        public bool IsShown(MapEditorSubTab row, bool restrictedPalette)
        {
            if (row == null || (restrictedPalette && row.SandboxRestricted))
                return false;

            if (row.Id == MapEditorSubcategory.Rooms)
                return true;

            return Query(row.Mode, row.Id, null).Any();
        }

        private static MapEditorCatalogEntry Virtual(string assetName, MapEditorSubcategory subTab, params string[] tags) =>
            new()
            {
                AssetName = assetName,
                Mode = MapEditorSubTabs.Get(subTab).Mode,
                Subcategory = subTab,
                SearchTags = tags,
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
