using NUnit.Framework;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.MapEditor;
using SS3D.Systems.Tile.MapEditor.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SS3D.Tests.EditMode
{
    /// <summary>
    /// Palette layout S6b (variant slots): one slot per family, cycling, per-family session memory and the
    /// V hint (<c>2026-11_map-editor-palette-layout.md</c> S6 decisions 1, 5, 6, 8–10).
    /// </summary>
    public sealed class MapEditorPaletteVariantTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void GroupFamilies_FoldsAFamilyWhereItsFirstMemberStood_BaseFirst()
        {
            MapEditorCatalogEntry[] entries =
            {
                Entry("Bed", MapEditorSubcategory.Furniture),
                Entry("OfficeChairBlue", MapEditorSubcategory.Furniture, "Office Chair", "Blue"),
                Entry("OfficeChair", MapEditorSubcategory.Furniture, "Office Chair", "Standard"),
                Entry("Sink", MapEditorSubcategory.Furniture),
                Entry("OfficeChairGrey", MapEditorSubcategory.Furniture, "Office Chair", "Grey"),
            };

            IReadOnlyList<MapEditorPaletteSlot> slots = MapEditorCatalog.GroupFamilies(entries);

            CollectionAssert.AreEqual(new[] { "Bed", "OfficeChair", "Sink" },
                slots.Select(slot => slot.Variants[0].AssetName).ToArray());
            Assert.IsTrue(slots[1].IsFamily);
            CollectionAssert.AreEqual(new[] { "OfficeChair", "OfficeChairBlue", "OfficeChairGrey" },
                slots[1].Variants.Select(variant => variant.AssetName).ToArray());
            Assert.IsFalse(slots[0].IsFamily);
        }

        [Test]
        public void Step_WrapsBothWays()
        {
            MapEditorPaletteSlot slot = MapEditorCatalog.GroupFamilies(new[]
            {
                Entry("FilterL", MapEditorSubcategory.GasPipes, "Filter", "Left"),
                Entry("FilterR", MapEditorSubcategory.GasPipes, "Filter", "Right"),
                Entry("FilterX", MapEditorSubcategory.GasPipes, "Filter", "X"),
            })[0];

            Assert.AreEqual("FilterR", slot.Step(slot.Variants[0], 1).AssetName);
            Assert.AreEqual("FilterX", slot.Step(slot.Variants[0], -1).AssetName);
            Assert.AreEqual("FilterL", slot.Step(slot.Variants[2], 1).AssetName);
        }

        [Test]
        public void ShownVariant_IsTheBase_ThenTheFamilysLastPick_ForTheSession()
        {
            MapEditorCatalog catalog = Synthetic(
                Entry("PlantEarth", MapEditorSubcategory.Furniture, "Plant", "Earth"),
                Entry("PlantExotic", MapEditorSubcategory.Furniture, "Plant", "Exotic"),
                Entry("Sink", MapEditorSubcategory.Furniture));
            MapEditorViewModel vm = new() { Catalog = catalog };
            MapEditorPaletteSlot plant = catalog.SlotOf(catalog.Entries[0]);

            Assert.AreEqual("PlantEarth", vm.ShownVariant(plant).AssetName);

            vm.SelectEntry(plant.Variants[1], null);
            Assert.AreEqual("PlantExotic", vm.ShownVariant(plant).AssetName);

            // Picking something else keeps the family's memory.
            vm.SelectEntry(catalog.Entries[2], null);
            vm.ClearSelection();
            Assert.AreEqual("PlantExotic", vm.ShownVariant(plant).AssetName);
        }

        [Test]
        public void Hint_OffersV_OnlyWhileAFamilyIsHeld()
        {
            MapEditorCatalog catalog = Synthetic(
                Entry("PlantEarth", MapEditorSubcategory.Furniture, "Plant", "Earth"),
                Entry("PlantExotic", MapEditorSubcategory.Furniture, "Plant", "Exotic"),
                Entry("Sink", MapEditorSubcategory.Furniture),
                // A family folder with one member draws as a plain entry, so V has nothing to cycle.
                Entry("Lonely", MapEditorSubcategory.Furniture, "Lonely", "Standard"));
            MapEditorViewModel vm = new() { Catalog = catalog };
            vm.SetTool(MapEditorTool.Edit);
            TileObjectSo asset = ScriptableObject.CreateInstance<TileObjectSo>();
            _created.Add(asset);

            vm.SelectEntry(catalog.Entries[0], asset);
            StringAssert.EndsWith("V: variant", vm.SelectedObjectHint);
            Assert.IsTrue(vm.HoldsFamily);

            vm.SelectEntry(catalog.Entries[2], asset);
            StringAssert.DoesNotContain("V: variant", vm.SelectedObjectHint);

            vm.SelectEntry(catalog.Entries[3], asset);
            Assert.IsFalse(vm.HoldsFamily);
        }

        [Test]
        public void SearchSlots_HoldTheWholeFamily_OncePerFamily()
        {
            MapEditorCatalog catalog = Synthetic(
                Entry("OfficeChair", MapEditorSubcategory.Furniture, "Office Chair", "Standard"),
                Entry("OfficeChairBlue", MapEditorSubcategory.Furniture, "Office Chair", "Blue"),
                Entry("OfficeChairGrey", MapEditorSubcategory.Furniture, "Office Chair", "Grey"),
                Entry("PlasticChairBlue", MapEditorSubcategory.Furniture, "Plastic Chair", "Blue"));

            IReadOnlyList<MapEditorPaletteSlot> slots = FurnitureSlots(catalog, "chair");

            CollectionAssert.AreEqual(new[] { "Office Chair", "Plastic Chair" }, slots.Select(slot => slot.Family).ToArray());
            Assert.AreEqual(3, FurnitureSlots(catalog, "blue")[0].Variants.Count);
        }

        [Test]
        public void CommittedFurniture_KeepsItsHeadings_WithFewerSlots()
        {
            var committed = AssetDatabase.LoadAssetAtPath<MapEditorCatalogSo>(MapEditorCatalogGeneration.CatalogPath);
            MapEditorCatalog catalog = new();
            catalog.Build(System.Array.Empty<GenericObjectSo>(), committed);
            MapEditorSubTab furniture = MapEditorSubTabs.Get(MapEditorSubcategory.Furniture);
            MapEditorCatalogEntry[] entries = catalog.Query(furniture.Mode, furniture.Id, null).ToArray();

            IReadOnlyList<MapEditorHeadingGroup> groups = MapEditorCatalog.GroupByHeading(furniture, entries);
            int slots = groups.Sum(group => MapEditorCatalog.GroupFamilies(group.Entries).Count);

            Assert.That(groups.Count, Is.GreaterThan(1), "Headings count entries, not slots (S6 decision 9)");
            Assert.That(slots, Is.LessThan(entries.Length));
        }

        // Decals and markers join every synthetic catalog, so take only the Furniture group.
        private static IReadOnlyList<MapEditorPaletteSlot> FurnitureSlots(MapEditorCatalog catalog, string term) =>
            catalog.SlotsFor(catalog.Search(term, restrictedPalette: false)
                .Single(group => group.SubTab.Id == MapEditorSubcategory.Furniture).Entries);

        private MapEditorCatalog Synthetic(params MapEditorCatalogEntry[] entries)
        {
            MapEditorCatalogSo catalogSo = ScriptableObject.CreateInstance<MapEditorCatalogSo>();
            _created.Add(catalogSo);
            catalogSo.Entries = entries.ToList();
            MapEditorCatalog catalog = new();
            catalog.Build(System.Array.Empty<GenericObjectSo>(), catalogSo);
            return catalog;
        }

        private static MapEditorCatalogEntry Entry(string name, MapEditorSubcategory subTab, string family = "", string variant = "") =>
            new()
            {
                AssetName = name,
                Mode = MapEditorSubTabs.Get(subTab).Mode,
                Subcategory = subTab,
                Family = family,
                VariantLabel = variant,
            };
    }
}
