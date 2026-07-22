using System;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Serializable catalog entry mapping an asset to a library slot.
    /// </summary>
    [Serializable]
    public sealed class MapEditorCatalogEntry
    {
        public string AssetName;
        public MapEditorMode Mode;
        public MapEditorSubcategory Subcategory;
        public string[] SearchTags = Array.Empty<string>();
        public bool IsEraser;
    }

    /// <summary>
    /// Version-controlled taxonomy for the map editor object library.
    /// </summary>
    [CreateAssetMenu(fileName = "MapEditorCatalog", menuName = "SS3D/Map Editor Catalog", order = 0)]
    public sealed class MapEditorCatalogSo : ScriptableObject
    {
        public List<MapEditorCatalogEntry> Entries = new();
    }
}
