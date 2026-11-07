#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityAssetDatabase = UnityEditor.AssetDatabase;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// What <c>SS3D/Map Editor/Regenerate Catalog</c> writes into <c>MapEditorCatalog.asset</c>. Lives in the
    /// Systems assembly so the EditMode drift test can compare the committed asset against it.
    /// </summary>
    public static class MapEditorCatalogGeneration
    {
        public const string CatalogPath = "Assets/Content/Systems/Tile/MapEditorCatalog.asset";

        /// <summary>
        /// Every tile asset under a <c>Resources</c> folder, filed by <see cref="MapEditorCatalogHeuristics"/>,
        /// in name order. Assets with no sub-tab are skipped with a warning.
        /// </summary>
        public static List<MapEditorCatalogEntry> BuildEntries()
        {
            List<MapEditorCatalogEntry> entries = new();
            var assets = Resources.LoadAll<GenericObjectSo>("")
                .Select(asset => (Asset: asset, Path: UnityAssetDatabase.GetAssetPath(asset)))
                .OrderBy(pair => pair.Asset.NameString, StringComparer.Ordinal)
                .ThenBy(pair => pair.Path, StringComparer.Ordinal);

            foreach ((GenericObjectSo asset, string path) in assets)
            {
                MapEditorCatalogEntry entry = MapEditorCatalogHeuristics.Infer(asset, path);
                if (entry == null)
                {
                    Debug.LogWarning($"[MapEditorCatalog] {path} has no palette sub-tab; move it or fix its layer.");
                    continue;
                }

                entries.Add(entry);
            }

            return entries;
        }
    }
}
#endif
