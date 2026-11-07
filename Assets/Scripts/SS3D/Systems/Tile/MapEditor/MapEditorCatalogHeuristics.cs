using SS3D.Systems.Tile.Connections;
using System;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Files a tile asset under its palette sub-tab. Tile objects go by the layer they sit on
    /// (+ <see cref="TileObjectGenericType"/> for turfs); items go by their folder under <c>Items/</c>.
    /// The regenerator bakes the result into <c>MapEditorCatalog.asset</c>. A misfiled asset is fixed
    /// by moving it, never by a name rule here.
    /// </summary>
    public static class MapEditorCatalogHeuristics
    {
        /// <summary>Furniture-layer folders that hold furniture; every other furniture-layer object is a machine.</summary>
        private static readonly string[] FurnitureFolders =
        {
            "Furniture/Generic/",
            "Furniture/Flora/",
            "Furniture/Storage/",
        };

        /// <summary><c>Items/</c> folder → sub-tab. First match wins; anything unlisted is Misc.</summary>
        private static readonly (string Folder, MapEditorSubcategory SubTab)[] ItemFolders =
        {
            ("Items/Clothing/", MapEditorSubcategory.Clothing),
            ("Items/Consumables/Chemical/", MapEditorSubcategory.Medical),
            ("Items/Functional/Medical/", MapEditorSubcategory.Medical),
            ("Items/Consumables/Food/", MapEditorSubcategory.FoodAndDrink),
            ("Items/Functional/Tools/", MapEditorSubcategory.Tools),
            ("Items/Functional/Materials/", MapEditorSubcategory.MaterialsAndParts),
            ("Items/Functional/Parts/", MapEditorSubcategory.MaterialsAndParts),
            ("Items/Functional/Devices/", MapEditorSubcategory.Devices),
            ("Items/Weapons/", MapEditorSubcategory.Weapons),
        };

        /// <summary>
        /// Sub-tab → the folders its headings come from (creative-mode §2, decision 12): the folder just below
        /// a root is the heading, and a file directly in a root, or outside every root, is under <see cref="GeneralHeading"/>.
        /// A sub-tab with no roots has no headings. Rename a folder to change a heading; never add a rule here.
        /// </summary>
        private static readonly (MapEditorSubcategory SubTab, string[] Roots)[] HeadingRoots =
        {
            (MapEditorSubcategory.Furniture, FurnitureFolders),
            (MapEditorSubcategory.Machines, new[] { "Furniture/Machines/" }),
        };

        public const string GeneralHeading = "General";

        private const string ResourcesRoot = "/Resources/";

        public static MapEditorCatalogEntry Infer(GenericObjectSo asset) =>
            Infer(asset, assetPath: null);

        /// <param name="assetPath">
        /// Unity asset path (<c>AssetDatabase.GetAssetPath</c>). Without it (runtime fallback for an
        /// asset missing from the baked catalog) items land in Misc and furniture-layer objects in Machines.
        /// </param>
        /// <returns>Null when the asset has no sub-tab (a turf of an unknown kind, or not a tile asset).</returns>
        public static MapEditorCatalogEntry Infer(GenericObjectSo asset, string assetPath)
        {
            if (asset == null)
                return null;

            string assetName = ResolveAssetName(asset);

            if (asset is ItemObjectSo)
                return Entry(assetName, ClassifyItem(assetPath), assetPath, "item");

            if (asset is not TileObjectSo tile)
                return null;

            MapEditorSubcategory? subTab = ClassifyTile(tile.layer, tile.genericType, assetPath);
            return subTab.HasValue ? Entry(assetName, subTab.Value, assetPath) : null;
        }

        public static MapEditorSubcategory? ClassifyTile(TileLayer layer, TileObjectGenericType genericType, string assetPath = null) =>
            layer switch
            {
                TileLayer.Turf => genericType switch
                {
                    TileObjectGenericType.Floor => MapEditorSubcategory.Floors,
                    TileObjectGenericType.Wall => MapEditorSubcategory.WallsAndWindows,
                    TileObjectGenericType.Door => MapEditorSubcategory.Doors,
                    _ => null,
                },
                TileLayer.WallMountLow or TileLayer.WallMountHigh => MapEditorSubcategory.WallMounts,
                TileLayer.FurnitureBase or TileLayer.FurnitureTop => IsUnderAny(assetPath, FurnitureFolders)
                    ? MapEditorSubcategory.Furniture
                    : MapEditorSubcategory.Machines,
                TileLayer.Plenum => MapEditorSubcategory.Subfloor,
                TileLayer.Wire => MapEditorSubcategory.Cables,
                TileLayer.PipeLeft or TileLayer.PipeRight or TileLayer.PipeMiddle
                    or TileLayer.PipeSurface or TileLayer.PipeManifold => MapEditorSubcategory.GasPipes,
                TileLayer.Disposal => MapEditorSubcategory.DisposalPipes,
                _ => null,
            };

        /// <summary>The item's sub-tab from its folder under <c>Items/</c>; Misc when unlisted or unknown.</summary>
        public static MapEditorSubcategory ClassifyItem(string assetPath)
        {
            string relative = RelativeToResources(assetPath);
            foreach ((string folder, MapEditorSubcategory subTab) in ItemFolders)
            {
                if (relative.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                    return subTab;
            }

            return MapEditorSubcategory.Misc;
        }

        /// <summary>
        /// The grid heading an asset sits under in its sub-tab: "Furniture/Machines/Power/SMES.asset" → "Power".
        /// Empty for a sub-tab without heading roots.
        /// </summary>
        public static string HeadingOf(MapEditorSubcategory subTab, string assetPath)
        {
            string[] roots = null;
            foreach ((MapEditorSubcategory headed, string[] headedRoots) in HeadingRoots)
            {
                if (headed == subTab)
                    roots = headedRoots;
            }

            if (roots == null)
                return string.Empty;

            string relative = RelativeToResources(assetPath);
            foreach (string root in roots)
            {
                if (!relative.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    continue;

                int folderEnd = relative.IndexOf('/', root.Length);
                return folderEnd > root.Length
                    ? MapEditorCatalog.PaletteLabel(relative.Substring(root.Length, folderEnd - root.Length))
                    : GeneralHeading;
            }

            return GeneralHeading;
        }

        private static bool IsUnderAny(string assetPath, string[] folders)
        {
            string relative = RelativeToResources(assetPath);
            foreach (string folder in folders)
            {
                if (relative.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>"Assets/…/TileMap/Resources/Items/Weapons/M4.asset" → "Items/Weapons/M4.asset".</summary>
        private static string RelativeToResources(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            string path = assetPath.Replace('\\', '/');
            int root = path.LastIndexOf(ResourcesRoot, StringComparison.OrdinalIgnoreCase);
            return root >= 0 ? path.Substring(root + ResourcesRoot.Length) : path;
        }

        private static string ResolveAssetName(GenericObjectSo asset)
        {
            if (asset.PrefabAsset != null)
                return asset.NameString;

            return string.IsNullOrEmpty(asset.name) ? string.Empty : asset.name;
        }

        private static MapEditorCatalogEntry Entry(string name, MapEditorSubcategory subTab, string assetPath, string extraTag = null)
        {
            MapEditorSubTab row = MapEditorSubTabs.Get(subTab);
            string label = row.Label.ToLowerInvariant();
            return new MapEditorCatalogEntry
            {
                AssetName = name,
                Mode = row.Mode,
                Subcategory = subTab,
                Heading = HeadingOf(subTab, assetPath),
                SearchTags = extraTag == null ? new[] { label } : new[] { extraTag, label },
            };
        }
    }
}
