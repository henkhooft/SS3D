using SS3D.Data.AssetDatabases;
using SS3D.Systems.Tile.TileMapCreator;
using System;
using System.Collections.Generic;

namespace SS3D.Systems.Tile.MapEditor.UI
{
    /// <summary>
    /// View-model state for the map editor UI.
    /// </summary>
    public sealed class MapEditorViewModel
    {
        public MapEditorTool CurrentTool { get; set; } = MapEditorTool.Edit;
        public MapEditorMode CurrentMode { get; set; } = MapEditorMode.Upper;
        public MapEditorSubcategory CurrentSubcategory { get; set; } = MapEditorSubcategory.Flooring;
        public string SearchText { get; set; } = string.Empty;
        public GenericObjectSo SelectedAsset { get; set; }
        public MapEditorCatalogEntry SelectedEntry { get; set; }
        public bool IsEraserSelected { get; set; }
        public bool HideUI { get; set; }
        public bool GridSnap { get; set; } = true;
        public bool DebugOverlay { get; set; }
        public float CameraFov { get; set; } = 72f;
        public float CameraZoomSpeed { get; set; } = 5f;
        public float CameraRotationSpeed { get; set; } = 4f;
        public int UndoDepth { get; set; }
        public int RedoDepth { get; set; }
        public string SelectedObjectHint { get; set; } = "Select the Edit tool to place this object";
        public string ToastMessage { get; set; }
        public string OpenPopover { get; set; }
        public Direction CurrentRotation { get; set; } = Direction.North;
        public MapEditorSaveTarget SaveTarget { get; set; } = MapEditorSaveTarget.LocalTemplate;
        public MapEditorPlacementMode PlacementMode { get; set; } = MapEditorPlacementMode.Normal;

        private readonly Dictionary<TileLayerCategory, bool> _layerCategoryVisibility = new();

        public event Action StateChanged;

        public void EnsureLayerDefaults()
        {
            foreach (TileLayerCategory category in TileLayerCategoryMapping.AllCategories)
            {
                if (!_layerCategoryVisibility.ContainsKey(category))
                    _layerCategoryVisibility[category] = true;
            }
        }

        public bool IsLayerCategoryVisible(TileLayerCategory category) =>
            !_layerCategoryVisibility.TryGetValue(category, out bool visible) || visible;

        public void SetLayerCategoryVisible(TileLayerCategory category, bool visible)
        {
            _layerCategoryVisibility[category] = visible;
            NotifyChanged();
        }

        public void NotifyChanged() => StateChanged?.Invoke();

        public void SetTool(MapEditorTool tool)
        {
            CurrentTool = tool;
            UpdateHint();
            NotifyChanged();
        }

        public void SetMode(MapEditorMode mode)
        {
            CurrentMode = mode;
            MapEditorSubcategory[] subs = MapEditorCatalog.GetSubcategories(mode);
            CurrentSubcategory = subs.Length > 0 ? subs[0] : MapEditorSubcategory.Uncategorized;
            NotifyChanged();
        }

        public void SetSubcategory(MapEditorSubcategory subcategory)
        {
            CurrentSubcategory = subcategory;
            NotifyChanged();
        }

        public void SelectEntry(MapEditorCatalogEntry entry, GenericObjectSo asset)
        {
            SelectedEntry = entry;
            SelectedAsset = asset;
            IsEraserSelected = entry?.IsEraser == true;
            UpdateHint();
            NotifyChanged();
        }

        public void ClearSelection()
        {
            SelectedEntry = null;
            SelectedAsset = null;
            IsEraserSelected = false;
            UpdateHint();
            NotifyChanged();
        }

        public void ShowToast(string message)
        {
            ToastMessage = message;
            NotifyChanged();
        }

        public void ClearToast()
        {
            ToastMessage = null;
            NotifyChanged();
        }

        public void SetUndoState(int undoDepth, int redoDepth)
        {
            UndoDepth = undoDepth;
            RedoDepth = redoDepth;
            NotifyChanged();
        }

        private void UpdateHint()
        {
            if (IsEraserSelected && CurrentTool == MapEditorTool.Edit)
            {
                SelectedObjectHint = "Edit tool active — click a tile to erase";
                return;
            }

            if (CurrentTool == MapEditorTool.Edit && SelectedAsset != null)
            {
                SelectedObjectHint = "Edit tool active — click a tile to place";
                return;
            }

            if (CurrentTool == MapEditorTool.Select)
            {
                SelectedObjectHint = "Select tool active — click a tile to inspect";
                return;
            }

            if (CurrentTool == MapEditorTool.Move)
            {
                SelectedObjectHint = "Move tool active — click and drag an object";
                return;
            }

            SelectedObjectHint = SelectedAsset != null
                ? "Select the Edit tool to place this object"
                : "Choose an object from the library";
        }
    }
}
