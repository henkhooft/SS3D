using NUnit.Framework;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.Connections;
using SS3D.Systems.Tile.MapEditor;
using SS3D.Systems.Tile.MapEditor.UI;
using SS3D.Systems.Tile.SpawnPoints;
using SS3D.Systems.Tile.Validation;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SS3D.Tests.EditMode
{
    public sealed class MapEditorCatalogMappingTests
    {
        private const string Res = "Assets/Content/Data/TileMap/Resources/";

        // Layer-first (Decisions 1–2): the layer decides, then the turf kind, then — for furniture
        // layers only — whether the asset sits under a furniture folder.
        [TestCase(TileLayer.Turf, TileObjectGenericType.Floor, "Structures/Floors/Tiles/TileWhite.asset", MapEditorSubcategory.Floors)]
        [TestCase(TileLayer.Turf, TileObjectGenericType.Wall, "Structures/Walls/SteelWindow.asset", MapEditorSubcategory.WallsAndWindows)]
        [TestCase(TileLayer.Turf, TileObjectGenericType.Door, "Structures/Doors/AirlockCivilian.asset", MapEditorSubcategory.Doors)]
        [TestCase(TileLayer.WallMountLow, TileObjectGenericType.None, "Structures/WallMounts/APC.asset", MapEditorSubcategory.WallMounts)]
        [TestCase(TileLayer.WallMountHigh, TileObjectGenericType.None, "Structures/WallMounts/Lights/LightTubeFixture.asset", MapEditorSubcategory.WallMounts)]
        [TestCase(TileLayer.FurnitureBase, TileObjectGenericType.None, "Furniture/Generic/Sink.asset", MapEditorSubcategory.Furniture)]
        [TestCase(TileLayer.FurnitureBase, TileObjectGenericType.None, "Furniture/Flora/PottedPlants/Plant0.asset", MapEditorSubcategory.Furniture)]
        [TestCase(TileLayer.FurnitureTop, TileObjectGenericType.None, "Furniture/Storage/Lockers/Locker.asset", MapEditorSubcategory.Furniture)]
        [TestCase(TileLayer.FurnitureBase, TileObjectGenericType.None, "Furniture/Machines/Power/SMES.asset", MapEditorSubcategory.Machines)]
        [TestCase(TileLayer.FurnitureBase, TileObjectGenericType.None, "Furniture/Machines/General/NukeCart.asset", MapEditorSubcategory.Machines)]
        [TestCase(TileLayer.FurnitureBase, TileObjectGenericType.None, "Structures/Pipes/Atmospherics/AtmosMeter.asset", MapEditorSubcategory.Machines)]
        [TestCase(TileLayer.FurnitureBase, TileObjectGenericType.None, null, MapEditorSubcategory.Machines)]
        [TestCase(TileLayer.Plenum, TileObjectGenericType.Plenum, "Structures/Floors/Plenum.asset", MapEditorSubcategory.Subfloor)]
        [TestCase(TileLayer.Plenum, TileObjectGenericType.None, "Structures/Floors/Catwalk.asset", MapEditorSubcategory.Subfloor)]
        [TestCase(TileLayer.Wire, TileObjectGenericType.Wire, "Structures/Wires/Cables.asset", MapEditorSubcategory.Cables)]
        [TestCase(TileLayer.PipeMiddle, TileObjectGenericType.Pipe, "Structures/Pipes/Atmospherics/AtmosPipe.asset", MapEditorSubcategory.GasPipes)]
        [TestCase(TileLayer.PipeSurface, TileObjectGenericType.None, "Furniture/Machines/Atmos/GasPump.asset", MapEditorSubcategory.GasPipes)]
        [TestCase(TileLayer.PipeManifold, TileObjectGenericType.None, "Structures/Pipes/Atmospherics/AtmosManifold4.asset", MapEditorSubcategory.GasPipes)]
        [TestCase(TileLayer.Disposal, TileObjectGenericType.Disposal, "Structures/Pipes/Disposals/DisposalPipes.asset", MapEditorSubcategory.DisposalPipes)]
        public void ClassifyTile_ByLayer(TileLayer layer, TileObjectGenericType genericType, string path, MapEditorSubcategory expected)
        {
            Assert.AreEqual(expected, MapEditorCatalogHeuristics.ClassifyTile(layer, genericType, path == null ? null : Res + path));
        }

        [Test]
        public void ClassifyTile_UnknownTurfKind_HasNoSubTab()
        {
            Assert.IsNull(MapEditorCatalogHeuristics.ClassifyTile(TileLayer.Turf, TileObjectGenericType.None));
        }

        // The item folder map; the folder is the sub-tab, the name never matters.
        [TestCase("Items/Clothing/JumpsuitSecurity.asset", MapEditorSubcategory.Clothing)]
        [TestCase("Items/Consumables/Chemical/BrutePatch.asset", MapEditorSubcategory.Medical)]
        [TestCase("Items/Functional/Medical/Medkit.asset", MapEditorSubcategory.Medical)]
        [TestCase("Items/Consumables/Food/Drinks/Mug.asset", MapEditorSubcategory.FoodAndDrink)]
        [TestCase("Items/Functional/Tools/Engineering/Wrench.asset", MapEditorSubcategory.Tools)]
        [TestCase("Items/Functional/Materials/SteelSheet.asset", MapEditorSubcategory.MaterialsAndParts)]
        [TestCase("Items/Functional/Parts/PowerCells/PowerCell.asset", MapEditorSubcategory.MaterialsAndParts)]
        [TestCase("Items/Functional/Devices/PDA.asset", MapEditorSubcategory.Devices)]
        [TestCase("Items/Weapons/M4.asset", MapEditorSubcategory.Weapons)]
        [TestCase("Items/Cosmetic/BikeHorn.asset", MapEditorSubcategory.Misc)]
        [TestCase("Items/Functional/Generic/Tanks/OxygenTank.asset", MapEditorSubcategory.Misc)]
        [TestCase("Items/Functional/Generic/SecurityToolkit.asset", MapEditorSubcategory.Misc)]
        public void ClassifyItem_ByFolder(string path, MapEditorSubcategory expected)
        {
            Assert.AreEqual(expected, MapEditorCatalogHeuristics.ClassifyItem(Res + path));
        }

        [Test]
        public void Infer_Item_WithoutPath_IsMisc()
        {
            ItemObjectSo item = ScriptableObject.CreateInstance<ItemObjectSo>();
            item.name = "Crowbar";

            MapEditorCatalogEntry entry = MapEditorCatalogHeuristics.Infer(item);

            Assert.AreEqual(MapEditorMode.Items, entry.Mode);
            Assert.AreEqual(MapEditorSubcategory.Misc, entry.Subcategory);
            Object.DestroyImmediate(item);
        }

        [Test]
        public void Infer_EntryModeMatchesItsRow()
        {
            TileObjectSo wire = ScriptableObject.CreateInstance<TileObjectSo>();
            wire.layer = TileLayer.Wire;

            MapEditorCatalogEntry entry = MapEditorCatalogHeuristics.Infer(wire);

            Assert.AreEqual(MapEditorSubcategory.Cables, entry.Subcategory);
            Assert.AreEqual(MapEditorMode.Lower, entry.Mode);
            Object.DestroyImmediate(wire);
        }

        /// <summary>
        /// The committed asset is generated (Decision 4): it must equal what <c>SS3D/Map Editor/Regenerate Catalog</c>
        /// would write now. A failure means an asset moved or the heuristics changed without regenerating.
        /// </summary>
        [Test]
        public void CommittedCatalog_MatchesRegeneration()
        {
            var committed = AssetDatabase.LoadAssetAtPath<MapEditorCatalogSo>(MapEditorCatalogGeneration.CatalogPath);
            Assert.IsNotNull(committed, MapEditorCatalogGeneration.CatalogPath);

            CollectionAssert.AreEqual(
                MapEditorCatalogGeneration.BuildEntries().Select(Describe).ToArray(),
                committed.Entries.Select(Describe).ToArray(),
                "MapEditorCatalog.asset is stale: run SS3D/Map Editor/Regenerate Catalog and commit it.");
        }

        [Test]
        public void EveryTileAsset_HasASubTab()
        {
            GenericObjectSo[] assets = Resources.LoadAll<GenericObjectSo>("");
            string[] unfiled = assets
                .Where(asset => MapEditorCatalogHeuristics.Infer(asset, AssetDatabase.GetAssetPath(asset)) == null)
                .Select(AssetDatabase.GetAssetPath)
                .ToArray();

            CollectionAssert.IsEmpty(unfiled);
        }

        [Test]
        public void EveryRow_HasEntries_WithTheCommittedCatalog()
        {
            MapEditorCatalog catalog = BuildCommittedCatalog();

            foreach (MapEditorSubTab row in MapEditorSubTabs.All)
                Assert.IsTrue(catalog.IsShown(row, restrictedPalette: false), $"{MapEditorSubTabs.PathOf(row.Id)} is empty");
        }

        [Test]
        public void SubTabs_AreInTheDesignOrder()
        {
            CollectionAssert.AreEqual(
                new[] { "Floors", "Walls & Windows", "Doors", "Wall Mounts", "Furniture", "Machines", "Decals" },
                Labels(MapEditorMode.Upper));
            CollectionAssert.AreEqual(new[] { "Subfloor", "Cables", "Gas Pipes", "Disposal Pipes" }, Labels(MapEditorMode.Lower));
            CollectionAssert.AreEqual(
                new[] { "Tools", "Materials & Parts", "Clothing", "Medical", "Food & Drink", "Devices", "Weapons", "Misc" },
                Labels(MapEditorMode.Items));
            CollectionAssert.AreEqual(new[] { "Spawn Points", "Rooms", "Starting Air" }, Labels(MapEditorMode.Scripting));
            Assert.AreEqual("Scripts → Spawn Points", MapEditorSubTabs.PathOf(MapEditorSubcategory.SpawnPoints));
        }

        [Test]
        public void Sandbox_HidesSpawnPoints_AndOpensScriptsOnRooms()
        {
            MapEditorViewModel model = new() { Catalog = BuildCommittedCatalog() };
            model.SetCapabilities(MapEditorCapabilities.For(persistenceAllowed: false, isSandboxPlayer: true));

            model.SetMode(MapEditorMode.Scripting);

            CollectionAssert.DoesNotContain(model.ShownSubTabs(MapEditorMode.Scripting).Select(row => row.Id).ToArray(),
                MapEditorSubcategory.SpawnPoints);
            Assert.AreEqual(MapEditorSubcategory.Rooms, model.CurrentSubcategory);
        }

        [Test]
        public void EmptySubTab_IsNotShown()
        {
            MapEditorCatalog catalog = new();
            catalog.Build(System.Array.Empty<GenericObjectSo>(), null);

            Assert.IsFalse(catalog.IsShown(MapEditorSubTabs.Get(MapEditorSubcategory.Machines), restrictedPalette: false));
            Assert.IsTrue(catalog.IsShown(MapEditorSubTabs.Get(MapEditorSubcategory.Rooms), restrictedPalette: false));
            Assert.IsTrue(catalog.IsShown(MapEditorSubTabs.Get(MapEditorSubcategory.Decals), restrictedPalette: false));
        }

        [Test]
        public void Query_FiltersBySearchTerm()
        {
            var catalogSo = ScriptableObject.CreateInstance<MapEditorCatalogSo>();
            catalogSo.Entries = new List<MapEditorCatalogEntry>
            {
                new()
                {
                    AssetName = "SteelPlating",
                    Mode = MapEditorMode.Upper,
                    Subcategory = MapEditorSubcategory.Floors,
                    SearchTags = new[] { "plating", "floor" },
                },
            };

            MapEditorCatalog catalog = new();
            catalog.Build(System.Array.Empty<GenericObjectSo>(), catalogSo);

            Assert.AreEqual(1, catalog.Query(MapEditorMode.Upper, MapEditorSubcategory.Floors, "plating").Count());
            Object.DestroyImmediate(catalogSo);
        }

        /// <summary>Decision 7: Map Check advice names palette paths through <see cref="MapEditorSubTabs.PathOf"/>.</summary>
        [Test]
        public void MapValidator_HardCodesNoPaletteLabel()
        {
            string guid = AssetDatabase.FindAssets("MapValidator t:MonoScript").Single(
                id => Path.GetFileName(AssetDatabase.GUIDToAssetPath(id)) == "MapValidator.cs");
            string source = File.ReadAllText(AssetDatabase.GUIDToAssetPath(guid));
            // Text the player reads: string literals with interpolation holes removed.
            string[] literals = Regex.Matches(source, "\"(?:[^\"\\\\]|\\\\.)*\"")
                .Select(match => Regex.Replace(match.Value, "\\{[^}]*\\}", string.Empty))
                .ToArray();

            IEnumerable<string> labels = MapEditorSubTabs.All.Select(row => row.Label)
                .Concat(MapEditorSubTabs.Modes.Select(MapEditorSubTabs.ModeLabel))
                .Append("→");
            foreach (string label in labels)
            {
                foreach (string literal in literals)
                    StringAssert.DoesNotContain(label, literal, $"MapValidator hard-codes \"{label}\"; use MapEditorSubTabs.PathOf.");
            }
        }

        /// <summary>The grouped issues are what Map Check shows; they must say where the markers live.</summary>
        [Test]
        public void MapCheck_SendsMissingSpawnsToSpawnPoints()
        {
            MapValidationReport report = MapValidator.Validate("Nuclear Emergency", new[] { "Assistant" },
                new List<SpawnPointRecord>(), hasNuke: true, hasDisk: true);

            string[] bodies = report.Issues.Where(issue => issue.Id is "job-spawns" or "nuclear")
                .Select(issue => issue.Body).ToArray();
            Assert.AreEqual(2, bodies.Length);
            foreach (string body in bodies)
                StringAssert.Contains("Scripts → Spawn Points", body);
        }

        private static MapEditorCatalog BuildCommittedCatalog()
        {
            var committed = AssetDatabase.LoadAssetAtPath<MapEditorCatalogSo>(MapEditorCatalogGeneration.CatalogPath);
            MapEditorCatalog catalog = new();
            catalog.Build(System.Array.Empty<GenericObjectSo>(), committed);
            return catalog;
        }

        private static string[] Labels(MapEditorMode mode) => MapEditorSubTabs.InMode(mode).Select(row => row.Label).ToArray();

        private static string Describe(MapEditorCatalogEntry entry) =>
            $"{entry.AssetName} | {entry.Mode} | {entry.Subcategory} | {string.Join(",", entry.SearchTags ?? new string[0])}";
    }
}
