using NUnit.Framework;
using SS3D.Systems.IdAccess;
using SS3D.Systems.Tile;
using SS3D.Systems.Tile.TileMapCreator;
using SS3D.Systems.Area;
using SS3D.Systems.Tile.MapEditor;
using SS3D.Systems.Tile.MapEditor.UI;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace EditorTests
{
    public class MapEditorAreaViewTests
    {
        [Test]
        public void AreaLayerStartsHiddenAndFollowsToolSelection()
        {
            MapEditorViewModel model = new();
            Assert.IsFalse(model.IsLayerCategoryVisible(TileLayerCategory.Areas));
            model.EnsureLayerDefaults();
            Assert.IsFalse(model.IsLayerCategoryVisible(TileLayerCategory.Areas));
            model.SetTool(MapEditorTool.Area);
            Assert.IsTrue(model.IsLayerCategoryVisible(TileLayerCategory.Areas));
            model.SetTool(MapEditorTool.Edit);
            Assert.IsFalse(model.IsLayerCategoryVisible(TileLayerCategory.Areas));
        }

        [Test]
        public void AreaOverlayPreservesTilePositionsAndGizmoColors()
        {
            ushort[] ids = new ushort[TileConstants.ChunkSize * TileConstants.ChunkSize];
            ids[0] = 1;
            ids[TileConstants.ChunkSize + 2] = 2;
            Mesh mesh = MapEditorAreaOverlay.BuildChunkMesh(ids);
            try
            {
                Assert.AreEqual(8, mesh.vertexCount);
                Assert.AreEqual(12, mesh.triangles.Length);
                Assert.AreEqual(new Vector3(1.55f, 0, 0.55f), mesh.vertices[4]);
                Assert.AreEqual(AreaDevSettings.GetColorForArea(1), mesh.colors[0]);
                Assert.AreEqual(AreaDevSettings.GetColorForArea(2), mesh.colors[4]);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void DepartmentPreviewUpdatesReadOnlyAndPreservesCustomOverride()
        {
            MapEditorViewModel model = new();
            MapEditorView view = new(null, null, model, new MapEditorCatalog(), null);
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>();
            window.Show();
            try
            {
                view.Build(window.rootVisualElement);
                view.ShowAreaProperties(Vector3.zero, "Vault", "Command", (ulong)AccessLevel.Armory);
                Toggle defaults = view.Root.Q<Toggle>("area-default-access");
                Toggle command = view.Root.Q<Toggle>("area-access-Command");
                Toggle security = view.Root.Q<Toggle>("area-access-Security");
                Toggle armory = view.Root.Q<Toggle>("area-access-Armory");
                defaults.value = true;
                Assert.IsTrue(command.value);
                Assert.IsFalse(command.enabledSelf);
                view.Root.Q<DropdownField>("area-department").value = "Security";
                Assert.IsFalse(command.value);
                Assert.IsTrue(security.value);
                Assert.IsFalse(security.enabledSelf);
                defaults.value = false;
                Assert.IsTrue(armory.value);
                Assert.IsFalse(security.value);
                Assert.IsTrue(armory.enabledSelf);
            }
            finally { view.Destroy(); window.Close(); }
        }

        [Test]
        public void RoomLabelAnchorStaysOnAnActualTileForConcaveRooms()
        {
            Vector3[] tiles = { Vector3.zero, new Vector3(4, 0, 0), new Vector3(0, 0, 4) };
            Assert.Contains(MapEditorAreaOverlay.ChooseLabelPosition(tiles), tiles);
        }

        [Test]
        public void RoomNamesUpdateAndOldNamesClearOnMapReset()
        {
            AreaFloorVisualCache cache = new();
            cache.ReplaceNames(new ushort[] { 1 }, new[] { "Vault" });
            Assert.AreEqual("Vault", cache.GetAreaName(1));
            cache.ReplaceNames(new ushort[] { 1 }, new[] { "Bridge" });
            Assert.AreEqual("Bridge", cache.GetAreaName(1));
            cache.Clear();
            Assert.AreEqual("Area 1", cache.GetAreaName(1));
        }

        [Test]
        public void ScriptsAndRoomsTabsControlTheirLayers()
        {
            MapEditorViewModel model = new();
            model.EnsureLayerDefaults();
            Assert.IsFalse(model.IsLayerCategoryVisible(TileLayerCategory.Scripts));
            model.SetMode(MapEditorMode.Scripting);
            Assert.IsTrue(model.IsLayerCategoryVisible(TileLayerCategory.Scripts));
            Assert.IsFalse(model.IsLayerCategoryVisible(TileLayerCategory.Areas));
            model.SetSubcategory(MapEditorSubcategory.Rooms);
            Assert.AreEqual(MapEditorTool.Area, model.CurrentTool);
            Assert.IsTrue(model.IsLayerCategoryVisible(TileLayerCategory.Areas));
            model.SetSubcategory(MapEditorSubcategory.SpawnPlacements);
            Assert.AreEqual(MapEditorTool.Edit, model.CurrentTool);
            Assert.IsFalse(model.IsLayerCategoryVisible(TileLayerCategory.Areas));
            Assert.IsTrue(model.IsLayerCategoryVisible(TileLayerCategory.Scripts));
            model.SetMode(MapEditorMode.Upper);
            Assert.IsFalse(model.IsLayerCategoryVisible(TileLayerCategory.Scripts));
            Assert.IsFalse(model.IsLayerCategoryVisible(TileLayerCategory.Areas));
            CollectionAssert.Contains(MapEditorCatalog.GetSubcategories(MapEditorMode.Scripting), MapEditorSubcategory.Rooms);
            Assert.IsFalse(MapEditorCatalog.IsUnavailableScriptingSubcategory(MapEditorSubcategory.Rooms));
        }

        private static void Submit(Button button)
        {
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }

        [Test]
        public void DraftSurvivesToastAndUndoChromeRefresh()
        {
            MapEditorViewModel model = new();
            MapEditorView view = new(null, null, model, new MapEditorCatalog(), null);
            try
            {
                view.Build(new VisualElement());
                view.ShowAreaProperties(Vector3.zero, "Vault", "Command", 0);
                TextField field = view.Root.Q<TextField>("area-name");
                field.value = "Secure vault";
                model.ShowToast("Unrelated notification");
                model.SetUndoState(1, 0);
                Assert.AreSame(field, view.Root.Q<TextField>("area-name"));
                Assert.AreEqual("Secure vault", field.value);
            }
            finally { view.Destroy(); }
        }

        [Test]
        public void DepartmentDefaultAndExplicitAccessSubmitDifferentMasks()
        {
            MapEditorViewModel model = new();
            MapEditorView view = new(null, null, model, new MapEditorCatalog(), null);
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>();
            window.Show();
            ulong submitted = ulong.MaxValue;
            view.AreaApplyRequested += (_, _, _, bits) => submitted = bits;
            try
            {
                view.Build(window.rootVisualElement);
                view.ShowAreaProperties(Vector3.zero, "Vault", "Command", (ulong)AccessLevel.Armory);
                Submit(view.Root.Q<Button>("area-apply"));
                Assert.AreEqual((ulong)AccessLevel.Armory, submitted);
                view.Root.Q<Toggle>("area-default-access").value = true;
                Submit(view.Root.Q<Button>("area-apply"));
                Assert.AreEqual(0UL, submitted);
            }
            finally { view.Destroy(); window.Close(); }
        }
    }
}
