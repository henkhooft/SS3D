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

    /// <summary>One sub-tab's matches in a palette-wide search.</summary>
    public sealed class MapEditorSearchGroup
    {
        public MapEditorSearchGroup(MapEditorSubTab subTab, IReadOnlyList<MapEditorCatalogEntry> entries)
        {
            SubTab = subTab;
            Entries = entries;
        }

        public MapEditorSubTab SubTab { get; }
        public IReadOnlyList<MapEditorCatalogEntry> Entries { get; }

        /// <summary>Where the matches live, as the grid heads them: "Upper › Machines".</summary>
        public string Heading => $"{MapEditorSubTabs.ModeLabel(SubTab.Mode)} › {SubTab.Label}";
    }

    /// <summary>One heading's entries in a sub-tab's grid; <see cref="Heading"/> is null when the grid draws no headings.</summary>
    public sealed class MapEditorHeadingGroup
    {
        public MapEditorHeadingGroup(string heading, IReadOnlyList<MapEditorCatalogEntry> entries)
        {
            Heading = heading;
            Entries = entries;
        }

        public string Heading { get; }
        public IReadOnlyList<MapEditorCatalogEntry> Entries { get; }
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

            if (string.IsNullOrWhiteSpace(search))
            {
                // Marker rails other than Spawn Points list in kind order.
                return MapEditorSubTabs.TryGet(subcategory, out MapEditorSubTab row) && row.IsMarker
                    && subcategory != MapEditorSubcategory.SpawnPoints
                    ? query
                    : query.OrderBy(e => e.AssetName);
            }

            string term = search.Trim();
            return query
                .Where(e => MatchesSearch(e, term))
                .OrderBy(e => e.AssetName);
        }

        /// <summary>
        /// Palette-wide search (creative-mode §2, decision 9): matches in every tab, grouped by sub-tab in
        /// tab and build order. Sub-tabs the player can't see are left out; an empty term finds nothing.
        /// </summary>
        public IReadOnlyList<MapEditorSearchGroup> Search(string search, bool restrictedPalette)
        {
            List<MapEditorSearchGroup> groups = new();
            if (string.IsNullOrWhiteSpace(search))
                return groups;

            foreach (MapEditorMode mode in MapEditorSubTabs.Modes)
            {
                foreach (MapEditorSubTab row in MapEditorSubTabs.InMode(mode))
                {
                    if (restrictedPalette && row.SandboxRestricted)
                        continue;

                    MapEditorCatalogEntry[] matches = Query(mode, row.Id, search).ToArray();
                    if (matches.Length > 0)
                        groups.Add(new MapEditorSearchGroup(row, matches));
                }
            }

            return groups;
        }

        /// <summary>
        /// A sub-tab's grid split under its folder headings (creative-mode §2, decision 12): only once the sub-tab
        /// holds <see cref="MapEditorSubTab.HeadingThreshold"/> entries and they carry headings. Headings sort by name
        /// with <see cref="MapEditorCatalogHeuristics.GeneralHeading"/> last; entries keep their order. Otherwise one
        /// group with no heading.
        /// </summary>
        public static IReadOnlyList<MapEditorHeadingGroup> GroupByHeading(MapEditorSubTab row, IReadOnlyList<MapEditorCatalogEntry> entries)
        {
            if (row == null || entries.Count < row.HeadingThreshold || entries.All(e => string.IsNullOrEmpty(e.Heading)))
                return new[] { new MapEditorHeadingGroup(null, entries) };

            return entries
                .GroupBy(e => string.IsNullOrEmpty(e.Heading) ? MapEditorCatalogHeuristics.GeneralHeading : e.Heading,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => string.Equals(group.Key, MapEditorCatalogHeuristics.GeneralHeading, StringComparison.OrdinalIgnoreCase))
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new MapEditorHeadingGroup(group.Key, group.ToArray()))
                .ToArray();
        }

        /// <summary>
        /// The words that find <paramref name="assetName"/> in search: a marker's job, antagonist or kind,
        /// a decal's name, otherwise the asset name. Map Check's Show in palette searches for this.
        /// </summary>
        public static string SearchTermFor(string assetName)
        {
            if (MapEditorSpawnCatalog.TryDecode(assetName, out SpawnPointKind kind, out string jobName,
                    out AntagonistSpawnCategory category))
                return kind == SpawnPointKind.Job ? jobName : MapEditorSpawnCatalog.FormatAntagonist(category);

            if (MapEditorGasMarkerCatalog.TryDecode(assetName, out GasMarkerKind gasKind))
                return MapEditorGasMarkerCatalog.DisplayName(gasKind);

            if (MapEditorFloorDecalCatalog.TryDecode(assetName, out ushort decalId)
                && FloorDecalCatalog.Get()?.TryGet(decalId, out FloorDecalDefinition decal) == true)
                return decal.DisplayName;

            return assetName;
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

        /// <summary>
        /// An asset name as palette words, so the two-line slot label can wrap it: "AtmosPipesSupply" →
        /// "Atmos Pipes Supply", "IDCard" → "ID Card". Acronyms and trailing digits stay whole ("APC", "AtmosFilterL2").
        /// </summary>
        public static string PaletteLabel(string assetName) =>
            string.IsNullOrEmpty(assetName)
                ? assetName
                : System.Text.RegularExpressions.Regex.Replace(assetName, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");

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
