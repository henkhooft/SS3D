using NUnit.Framework;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.Connections;
using SS3D.Systems.Tile.MapEditor;
using UnityEngine;

namespace SS3D.Tests.EditMode
{
    public sealed class MapEditorCatalogMappingTests
    {
        [Test]
        public void Infer_WallObject_MapsToUpperWalls()
        {
            TileObjectSo wall = ScriptableObject.CreateInstance<TileObjectSo>();
            wall.layer = TileLayer.Turf;
            wall.genericType = TileObjectGenericType.Wall;

            MapEditorCatalogEntry entry = MapEditorCatalogHeuristics.Infer(wall);

            Assert.AreEqual(MapEditorMode.Upper, entry.Mode);
            Assert.AreEqual(MapEditorSubcategory.Walls, entry.Subcategory);
        }

        [Test]
        public void Infer_ItemObject_MapsToItemsMisc()
        {
            ItemObjectSo item = ScriptableObject.CreateInstance<ItemObjectSo>();

            MapEditorCatalogEntry entry = MapEditorCatalogHeuristics.Infer(item);

            Assert.AreEqual(MapEditorMode.Items, entry.Mode);
            Assert.AreEqual(MapEditorSubcategory.Misc, entry.Subcategory);
        }

        [Test]
        public void Query_FiltersBySearchTerm()
        {
            var catalogSo = ScriptableObject.CreateInstance<MapEditorCatalogSo>();
            catalogSo.Entries = new System.Collections.Generic.List<MapEditorCatalogEntry>
            {
                new()
                {
                    AssetName = "SteelPlating",
                    Mode = MapEditorMode.Upper,
                    Subcategory = MapEditorSubcategory.Flooring,
                    SearchTags = new[] { "plating", "floor" },
                },
            };

            MapEditorCatalog catalog = new();
            catalog.Build(System.Array.Empty<GenericObjectSo>(), catalogSo);

            int matches = 0;
            foreach (MapEditorCatalogEntry _ in catalog.Query(MapEditorMode.Upper, MapEditorSubcategory.Flooring, "plating"))
                matches++;

            Assert.AreEqual(1, matches);
        }

        [Test]
        public void Infer_OverlayObject_MapsToUpperOverlays()
        {
            TileObjectSo overlay = ScriptableObject.CreateInstance<TileObjectSo>();
            overlay.layer = TileLayer.Overlays;

            MapEditorCatalogEntry entry = MapEditorCatalogHeuristics.Infer(overlay);

            Assert.AreEqual(MapEditorMode.Upper, entry.Mode);
            Assert.AreEqual(MapEditorSubcategory.Overlays, entry.Subcategory);
        }

        [Test]
        public void ScriptingSubcategories_AreMarkedUnavailable()
        {
            Assert.IsTrue(MapEditorCatalog.IsScriptingSubcategory(MapEditorSubcategory.SpawnPlacements));
            Assert.IsTrue(MapEditorCatalog.IsScriptingSubcategory(MapEditorSubcategory.Triggers));
        }
    }
}
