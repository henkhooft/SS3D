using SS3D.Data.AssetDatabases;
using SS3D.Systems.Tile.MapEditor.Persistence;
using SS3D.Systems.Tile.TileMapCreator;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SS3D.Systems.Tile.MapEditor.UI
{
    /// <summary>
    /// UI Toolkit view for the full-screen map editor.
    /// </summary>
    public sealed class MapEditorView
    {
        /// <summary>Object library panel heights (px) for the three resize states.</summary>
        private static readonly Dictionary<PaletteMode, float> PaletteHeights = new()
        {
            [PaletteMode.Min] = 52f,
            [PaletteMode.Normal] = 260f,
            [PaletteMode.Max] = 460f,
        };

        /// <summary>Object library grid row count per resize state (columns scroll horizontally).</summary>
        private const int NormalGridRows = 2;
        private const int MaxGridRows = 4;

        private readonly StyleSheet _styleSheet;
        private readonly MapEditorIconsSo _icons;
        private readonly MapEditorViewModel _vm;
        private readonly MapEditorCatalog _catalog;
        private readonly TileResourceLoader _loader;

        private VisualElement _root;
        private VisualElement _hudLayer;
        private VisualElement _leftToolbar;
        private VisualElement _rightToolbar;
        private VisualElement _modeRail;
        private VisualElement _subcatRow;
        private VisualElement _paletteBody;
        private VisualElement _paletteRegion;
        private ScrollView _gridScroll;
        private VisualElement _grid;
        private TextField _searchField;
        private TextField _saveNameField;
        private Label _windowTitle;
        private Label _toast;
        private VisualElement _leftPopoverAnchor;
        private VisualElement _rightPopoverAnchor;
        private VisualElement _cameraPopoverAnchor;
        private VisualElement _activePopover;

        private PaletteMode _paletteMode = PaletteMode.Normal;

        private readonly Dictionary<MapEditorTool, Button> _toolButtons = new();
        private readonly Dictionary<MapEditorMode, Button> _modeButtons = new();
        private readonly Dictionary<MapEditorSubcategory, Button> _subcatButtons = new();

        private enum PaletteMode
        {
            Min,
            Normal,
            Max,
        }

        public MapEditorView(
            StyleSheet styleSheet,
            MapEditorIconsSo icons,
            MapEditorViewModel viewModel,
            MapEditorCatalog catalog,
            TileResourceLoader loader)
        {
            _styleSheet = styleSheet;
            _icons = icons;
            _vm = viewModel;
            _catalog = catalog;
            _loader = loader;
            _vm.StateChanged += Refresh;
        }

        public event Action<MapEditorTool> ToolSelected;
        public event Action UndoRequested;
        public event Action RedoRequested;
        public event Action<string> SaveAsRequested;
        public event Action<string> LoadMapRequested;
        public event Action<string> DeleteMapRequested;
        public event Action ResetViewRequested;
        public event Action ShowUIRequested;
        public event Action<bool> GridSnapChanged;
        public event Action<bool> DebugOverlayChanged;
        public event Action<float, float, float> CameraSettingsChanged;
        public event Action<int> CameraRotateRequested;
        public event Action<int> CameraZoomRequested;
        public event Action NewMapRequested;
        public event Action MapListRefreshRequested;
        public event Action<TileLayerCategory, bool> LayerCategoryVisibilityChanged;
        public event Action<MapEditorMode> ModeSelected;
        public event Action<MapEditorSubcategory> SubcategorySelected;
        public event Action<MapEditorCatalogEntry, GenericObjectSo> AssetSelected;
        public event Action<string> SearchChanged;

        public VisualElement Root => _root;

        public void Build(VisualElement parent)
        {
            _root = new VisualElement { name = "map-editor-root", pickingMode = PickingMode.Ignore };
            _root.AddToClassList("map-editor-root");
            _root.style.flexGrow = 1;

            if (_styleSheet != null)
                _root.styleSheets.Add(_styleSheet);

            _hudLayer = new VisualElement { name = "hud-layer", pickingMode = PickingMode.Ignore };
            _hudLayer.style.position = Position.Absolute;
            _hudLayer.style.left = 0;
            _hudLayer.style.top = 0;
            _hudLayer.style.right = 0;
            _hudLayer.style.bottom = 0;

            BuildLeftToolbar();
            BuildRightSide();
            BuildBottomDock();
            BuildRevealButton();

            _toast = new Label { name = "toast", pickingMode = PickingMode.Ignore };
            _toast.AddToClassList("map-editor-toast");
            _toast.style.display = DisplayStyle.None;
            _root.Add(_toast);

            _root.Add(_hudLayer);
            parent.Add(_root);
            Refresh();
        }

        public void Destroy()
        {
            _vm.StateChanged -= Refresh;
            _root?.RemoveFromHierarchy();
        }

        public void SetMouseOverUI(bool over) => _root?.EnableInClassList("map-editor-mouse-over", over);

        private void BuildLeftToolbar()
        {
            VisualElement region = CreateRegion("map-editor-region--top-left");
            _leftPopoverAnchor = region;

            _leftToolbar = CreateToolbarStrip(vertical: true);
            AddToolButton(_leftToolbar, MapEditorTool.Edit, "Edit");
            AddToolButton(_leftToolbar, MapEditorTool.Select, "Select");
            AddToolButton(_leftToolbar, MapEditorTool.Dropper, "Dropper (copy object)");
            AddToolButton(_leftToolbar, MapEditorTool.Delete, "Delete");
            AddSeparator(_leftToolbar, vertical: true);

            Button undo = CreateIconButton(_icons?.Undo, "Undo", () => UndoRequested?.Invoke());
            undo.name = "undo-btn";
            _leftToolbar.Add(undo);

            Button redo = CreateIconButton(_icons?.Redo, "Redo", () => RedoRequested?.Invoke());
            redo.name = "redo-btn";
            _leftToolbar.Add(redo);
            AddSeparator(_leftToolbar, vertical: true);

            _leftToolbar.Add(CreateIconButton(_icons?.SaveMap, "Save map...", () => TogglePopover("saveMenu")));
            _leftToolbar.Add(CreateIconButton(_icons?.OpenMap, "Open map selection", () => TogglePopover("maps")));

            region.Add(_leftToolbar);
            _hudLayer.Add(region);
        }

        private void BuildRightSide()
        {
            VisualElement region = CreateRegion("map-editor-region--top-right");
            region.AddToClassList("map-editor-right-column");

            VisualElement cameraWrap = new() { pickingMode = PickingMode.Position };
            cameraWrap.style.position = Position.Relative;
            _cameraPopoverAnchor = cameraWrap;
            cameraWrap.Add(BuildCameraDial());
            region.Add(cameraWrap);

            VisualElement toolbarWrap = new() { pickingMode = PickingMode.Position };
            toolbarWrap.style.position = Position.Relative;
            _rightPopoverAnchor = toolbarWrap;

            _rightToolbar = CreateToolbarStrip(vertical: true);
            _rightToolbar.Add(CreateIconButton(_icons?.ResetView, "Reset position", () => ResetViewRequested?.Invoke()));
            _rightToolbar.Add(CreateIconButton(_icons?.Layers, "Layer view mode", () => TogglePopover("layers")));
            _rightToolbar.Add(CreateIconButton(_icons?.Settings, "Map editor settings", () => TogglePopover("settings")));
            toolbarWrap.Add(_rightToolbar);
            region.Add(toolbarWrap);

            _hudLayer.Add(region);
        }

        private VisualElement BuildCameraDial()
        {
            VisualElement dial = new() { pickingMode = PickingMode.Position };
            dial.AddToClassList("map-editor-camera-dial");

            VisualElement ring = new() { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("map-editor-camera-dial__ring");
            ring.name = "camera-dial-ring";
            dial.Add(ring);

            // Dedicated dial button — avoid map-editor-icon-btn's fixed 38px size,
            // which fights absolute centering inside the 56px dial.
            Button open = new(() => TogglePopover("camera")) { tooltip = "Camera options", name = "camera-dial-open" };
            open.AddToClassList("map-editor-camera-dial__open");
            VisualElement cameraIcon = CreateIconElement(_icons?.Camera);
            open.Add(cameraIcon);
            dial.Add(open);

            dial.Add(CreateDialButton(_icons?.RotateLeft, "Rotate left",
                () => CameraRotateRequested?.Invoke(-1), "map-editor-camera-dial__rotate-left"));
            dial.Add(CreateDialButton(_icons?.RotateRight, "Rotate right",
                () => CameraRotateRequested?.Invoke(1), "map-editor-camera-dial__rotate-right"));

            VisualElement zoomRow = new() { pickingMode = PickingMode.Position };
            zoomRow.AddToClassList("map-editor-camera-dial__zoom-row");
            zoomRow.Add(CreateDialButton(_icons?.ZoomOut, "Zoom out", () => CameraZoomRequested?.Invoke(-1)));
            zoomRow.Add(CreateDialButton(_icons?.ZoomIn, "Zoom in", () => CameraZoomRequested?.Invoke(1)));

            VisualElement wrap = new() { pickingMode = PickingMode.Position };
            wrap.AddToClassList("map-editor-camera-dial-wrap");
            wrap.Add(dial);
            wrap.Add(zoomRow);
            return wrap;
        }

        private static Button CreateDialButton(VectorImage icon, string tooltip, Action onClick, string extraClass = null)
        {
            Button btn = new(onClick) { tooltip = tooltip };
            btn.AddToClassList("map-editor-camera-dial__btn");
            if (extraClass != null)
                btn.AddToClassList(extraClass);

            VisualElement iconElement = CreateIconElement(icon);
            iconElement.AddToClassList("map-editor-camera-dial__btn-icon");
            btn.Add(iconElement);
            return btn;
        }

        private void BuildBottomDock()
        {
            VisualElement region = CreateRegion("map-editor-region--bottom");
            region.AddToClassList("map-editor-bottom-row");
            _paletteRegion = region;

            _modeRail = new VisualElement { pickingMode = PickingMode.Position };
            _modeRail.AddToClassList("map-editor-mode-rail");
            foreach (MapEditorMode mode in Enum.GetValues(typeof(MapEditorMode)))
            {
                Button tab = CreateModeTab(mode);
                _modeButtons[mode] = tab;
                _modeRail.Add(tab);
            }

            VisualElement library = new() { pickingMode = PickingMode.Position };
            library.AddToClassList("map-editor-library");
            VisualElement libWindow = CreateWindow(string.Empty);
            libWindow.style.flexGrow = 1;
            libWindow.style.paddingTop = 0;
            libWindow.style.paddingBottom = 0;
            libWindow.style.paddingLeft = 0;
            libWindow.style.paddingRight = 0;

            VisualElement header = new() { pickingMode = PickingMode.Position };
            header.AddToClassList("map-editor-library-header");
            _windowTitle = new Label { pickingMode = PickingMode.Ignore };
            _windowTitle.AddToClassList("map-editor-library-header__title");
            header.Add(_windowTitle);

            VisualElement headerButtons = new() { pickingMode = PickingMode.Position };
            headerButtons.AddToClassList("map-editor-library-header__buttons");
            Button collapseBtn = CreateHeaderIconButton(_icons?.PanelCollapse, "Minimize panel",
                () => TogglePaletteMode(PaletteMode.Min));
            collapseBtn.name = "palette-collapse-btn";
            Button expandBtn = CreateHeaderIconButton(_icons?.PanelExpand, "Expand panel",
                () => TogglePaletteMode(PaletteMode.Max));
            expandBtn.name = "palette-expand-btn";
            headerButtons.Add(collapseBtn);
            headerButtons.Add(expandBtn);
            header.Add(headerButtons);

            _paletteBody = new VisualElement { pickingMode = PickingMode.Position };
            _paletteBody.AddToClassList("map-editor-library-body");

            VisualElement toolsRow = new() { pickingMode = PickingMode.Position };
            toolsRow.style.flexDirection = FlexDirection.Row;
            toolsRow.style.justifyContent = Justify.SpaceBetween;

            _subcatRow = new VisualElement();
            _subcatRow.style.flexDirection = FlexDirection.Row;
            _subcatRow.style.flexWrap = Wrap.Wrap;

            VisualElement search = new();
            search.AddToClassList("map-editor-search");
            _searchField = new TextField { value = string.Empty };
            _searchField.AddToClassList("map-editor-search-field");
            _searchField.RegisterValueChangedCallback(evt => SearchChanged?.Invoke(evt.newValue));
            VisualElement searchIcon = CreateIconElement(_icons?.Search);
            searchIcon.AddToClassList("map-editor-search-icon");
            search.Add(searchIcon);
            search.Add(_searchField);

            toolsRow.Add(_subcatRow);
            toolsRow.Add(search);

            _gridScroll = new ScrollView(ScrollViewMode.Horizontal);
            _gridScroll.AddToClassList("map-editor-grid-scroll");
            _grid = new VisualElement();
            _grid.AddToClassList("map-editor-grid");
            _gridScroll.Add(_grid);

            _paletteBody.Add(toolsRow);
            _paletteBody.Add(_gridScroll);

            libWindow.Add(header);
            libWindow.Add(_paletteBody);
            library.Add(libWindow);

            region.Add(_modeRail);
            region.Add(library);
            _hudLayer.Add(region);
        }

        private void BuildRevealButton()
        {
            Button reveal = CreateIconButton(_icons?.ShowUi, "Show UI", () => ShowUIRequested?.Invoke());
            reveal.AddToClassList("map-editor-reveal-btn");
            reveal.name = "reveal-ui-btn";
            reveal.style.display = DisplayStyle.None;
            _root.Add(reveal);
        }

        private void TogglePaletteMode(PaletteMode extreme)
        {
            _paletteMode = _paletteMode == extreme ? PaletteMode.Normal : extreme;
            ApplyPaletteMode();
        }

        private void ApplyPaletteMode()
        {
            if (_paletteRegion == null)
                return;

            _paletteRegion.style.height = PaletteHeights[_paletteMode];
            bool showBody = _paletteMode != PaletteMode.Min;
            _paletteBody.style.display = showBody ? DisplayStyle.Flex : DisplayStyle.None;
            _modeRail.style.display = showBody ? DisplayStyle.Flex : DisplayStyle.None;

            Button collapseBtn = _paletteRegion.Q<Button>("palette-collapse-btn");
            collapseBtn?.EnableInClassList("map-editor-header-icon-btn--active", _paletteMode == PaletteMode.Min);
            Button expandBtn = _paletteRegion.Q<Button>("palette-expand-btn");
            expandBtn?.EnableInClassList("map-editor-header-icon-btn--active", _paletteMode == PaletteMode.Max);
            if (collapseBtn != null)
                collapseBtn.tooltip = _paletteMode == PaletteMode.Min ? "Restore panel" : "Minimize panel";
            if (expandBtn != null)
                expandBtn.tooltip = _paletteMode == PaletteMode.Max ? "Restore panel" : "Expand panel";

            RebuildGrid();
        }

        private void Refresh()
        {
            if (_root == null)
                return;

            _hudLayer.style.display = _vm.HideUI ? DisplayStyle.None : DisplayStyle.Flex;
            Button reveal = _root.Q<Button>("reveal-ui-btn");
            if (reveal != null)
                reveal.style.display = _vm.HideUI ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (KeyValuePair<MapEditorTool, Button> pair in _toolButtons)
            {
                pair.Value.EnableInClassList("map-editor-icon-btn--active", pair.Key == _vm.CurrentTool);
            }

            foreach (KeyValuePair<MapEditorMode, Button> pair in _modeButtons)
            {
                pair.Value.EnableInClassList("map-editor-mode-tab--active", pair.Key == _vm.CurrentMode);
            }

            RebuildSubcategories();
            ApplyPaletteMode();

            Button undoBtn = _leftToolbar?.Q<Button>("undo-btn");
            Button redoBtn = _leftToolbar?.Q<Button>("redo-btn");
            undoBtn?.EnableInClassList("map-editor-icon-btn--disabled", _vm.UndoDepth <= 0);
            redoBtn?.EnableInClassList("map-editor-icon-btn--disabled", _vm.RedoDepth <= 0);

            if (!string.IsNullOrEmpty(_vm.ToastMessage))
            {
                _toast.text = _vm.ToastMessage;
                _toast.style.display = DisplayStyle.Flex;
            }
            else
            {
                _toast.style.display = DisplayStyle.None;
            }

            RefreshPopover();
        }

        private void RebuildSubcategories()
        {
            _subcatRow.Clear();
            _subcatButtons.Clear();

            MapEditorSubcategory[] subs = MapEditorCatalog.GetSubcategories(_vm.CurrentMode);
            foreach (MapEditorSubcategory sub in subs)
            {
                Button tab = CreateSubcategoryTab(sub);
                _subcatButtons[sub] = tab;
                _subcatRow.Add(tab);
            }

            if (_windowTitle != null)
            {
                string mode = MapEditorCatalog.GetModeLabel(_vm.CurrentMode).ToUpperInvariant();
                string sub = MapEditorCatalog.GetSubcategoryLabel(_vm.CurrentSubcategory).ToUpperInvariant();
                _windowTitle.text = $"Object Library — {mode} / {sub}";
            }
        }

        private void RebuildGrid()
        {
            if (_grid == null || _paletteMode == PaletteMode.Min)
                return;

            _grid.Clear();

            if (_vm.CurrentMode == MapEditorMode.Scripting ||
                MapEditorCatalog.IsScriptingSubcategory(_vm.CurrentSubcategory))
            {
                Label stub = new(
                    "Not available yet — spawn and trigger authoring is a later pass.")
                {
                    style = { whiteSpace = WhiteSpace.Normal },
                };
                stub.AddToClassList("map-editor-stub-message");
                _grid.Add(stub);
                return;
            }

            IEnumerable<MapEditorCatalogEntry> entries =
                _catalog.Query(_vm.CurrentMode, _vm.CurrentSubcategory, _vm.SearchText);

            int rows = _paletteMode == PaletteMode.Max ? MaxGridRows : NormalGridRows;
            VisualElement column = null;
            int inColumn = 0;
            bool any = false;

            foreach (MapEditorCatalogEntry entry in entries)
            {
                any = true;
                if (column == null || inColumn >= rows)
                {
                    column = new VisualElement { pickingMode = PickingMode.Position };
                    column.AddToClassList("map-editor-grid-column");
                    _grid.Add(column);
                    inColumn = 0;
                }

                column.Add(BuildSlot(entry));
                inColumn++;
            }

            if (!any)
            {
                Label empty = new($"No matches for \"{_vm.SearchText}\".");
                empty.AddToClassList("map-editor-stub-message");
                _grid.Add(empty);
            }
        }

        private Button BuildSlot(MapEditorCatalogEntry entry)
        {
            GenericObjectSo asset = entry.IsEraser ? null : _loader.GetAsset(entry.AssetName);
            Button slot = new(() => AssetSelected?.Invoke(entry, asset));
            slot.AddToClassList("map-editor-slot");
            bool active = _vm.SelectedEntry != null &&
                          string.Equals(_vm.SelectedEntry.AssetName, entry.AssetName, StringComparison.OrdinalIgnoreCase);
            slot.EnableInClassList("map-editor-slot--active", active);

            VisualElement iconFrame = new();
            iconFrame.AddToClassList("map-editor-slot__icon-frame");

            VisualElement icon = new();
            icon.AddToClassList("map-editor-slot__icon");
            if (entry.IsEraser)
            {
                icon.AddToClassList("map-editor-slot__icon--eraser");
            }
            else if (asset?.icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(asset.icon);
            }

            iconFrame.Add(icon);

            Label label = new(entry.IsEraser ? "Eraser" : entry.AssetName)
            {
                style = { whiteSpace = WhiteSpace.Normal },
            };
            label.AddToClassList("map-editor-slot__label");

            slot.Add(iconFrame);
            slot.Add(label);
            return slot;
        }

        private void TogglePopover(string key)
        {
            _vm.OpenPopover = _vm.OpenPopover == key ? null : key;
            RefreshPopover();
        }

        private static bool IsLeftPopover(string key) => key is "maps" or "saveMenu";

        private void RefreshPopover()
        {
            _activePopover?.RemoveFromHierarchy();
            _activePopover = null;

            if (string.IsNullOrEmpty(_vm.OpenPopover))
                return;

            VisualElement anchor = _vm.OpenPopover switch
            {
                "maps" or "saveMenu" => _leftPopoverAnchor,
                "camera" => _cameraPopoverAnchor,
                _ => _rightPopoverAnchor,
            };

            VisualElement popover = new();
            popover.AddToClassList("map-editor-popover");
            popover.AddToClassList(IsLeftPopover(_vm.OpenPopover)
                ? "map-editor-popover--anchor-right"
                : "map-editor-popover--anchor-left");
            if (_vm.OpenPopover == "maps" || _vm.OpenPopover == "saveMenu")
                popover.AddToClassList("map-editor-popover--wide");

            VisualElement window = CreateWindow(GetPopoverTitle(_vm.OpenPopover));
            BuildPopoverContent(_vm.OpenPopover, window);
            popover.Add(window);

            Button close = new(() =>
            {
                _vm.OpenPopover = null;
                RefreshPopover();
            })
            { text = "Close" };
            close.style.marginTop = 8;
            window.Add(close);

            anchor.Add(popover);
            _activePopover = popover;

            if (_vm.OpenPopover == "maps" || _vm.OpenPopover == "saveMenu")
                RequestMapListRefresh();
        }

        public void RequestMapListRefresh() => MapListRefreshRequested?.Invoke();

        private static string GetPopoverTitle(string key) =>
            key switch
            {
                "maps" => "Map Selection",
                "saveMenu" => "Save Map",
                "layers" => "Layer View Mode",
                "camera" => "Camera Options",
                "settings" => "Map Editor Settings",
                _ => "Panel",
            };

        private void BuildPopoverContent(string key, VisualElement container)
        {
            switch (key)
            {
                case "maps":
                    BuildMapsPopover(container);
                    break;
                case "saveMenu":
                    BuildSavePopover(container);
                    break;
                case "layers":
                    _vm.EnsureLayerDefaults();
                    foreach (TileLayerCategory category in TileLayerCategoryMapping.AllCategories)
                    {
                        AddToggleRow(container, TileLayerCategoryMapping.GetDisplayName(category),
                            _vm.IsLayerCategoryVisible(category),
                            v => LayerCategoryVisibilityChanged?.Invoke(category, v));
                    }

                    break;
                case "camera":
                    AddSliderRow(container, "Field of View", _vm.CameraFov, 50f, 110f, "°", v =>
                    {
                        _vm.CameraFov = v;
                        RaiseCameraSettingsChanged();
                    });
                    AddSliderRow(container, "Zoom Speed", _vm.CameraZoomSpeed, 1f, 10f, string.Empty, v =>
                    {
                        _vm.CameraZoomSpeed = v;
                        RaiseCameraSettingsChanged();
                    });
                    AddSliderRow(container, "Rotation Speed", _vm.CameraRotationSpeed, 1f, 10f, string.Empty, v =>
                    {
                        _vm.CameraRotationSpeed = v;
                        RaiseCameraSettingsChanged();
                    });
                    break;
                case "settings":
                    AddToggleRow(container, "Grid Snap", _vm.GridSnap, v =>
                    {
                        _vm.GridSnap = v;
                        GridSnapChanged?.Invoke(v);
                    });
                    AddToggleRow(container, "Debug Overlay", _vm.DebugOverlay, v =>
                    {
                        _vm.DebugOverlay = v;
                        DebugOverlayChanged?.Invoke(v);
                    });
                    break;
            }
        }

        private void BuildMapsPopover(VisualElement container)
        {
            Button newMapBtn = new(() => NewMapRequested?.Invoke()) { text = "New Map" };
            newMapBtn.AddToClassList("map-editor-action-btn");
            newMapBtn.style.marginBottom = 8;
            container.Add(newMapBtn);
        }

        private void BuildSavePopover(VisualElement container)
        {
            VisualElement nameLabel = new Label("Save Name") { pickingMode = PickingMode.Ignore };
            nameLabel.AddToClassList("map-editor-popover-label");
            container.Add(nameLabel);

            VisualElement nameField = new() { pickingMode = PickingMode.Position };
            nameField.AddToClassList("map-editor-search");
            _saveNameField = new TextField { value = string.IsNullOrEmpty(_vm.SaveMapName) ? "Untitled Map" : _vm.SaveMapName };
            _saveNameField.AddToClassList("map-editor-search-field");
            _saveNameField.RegisterValueChangedCallback(evt => _vm.SaveMapName = evt.newValue);
            nameField.Add(_saveNameField);
            container.Add(nameField);

            Label overwriteLabel = new("Overwrite Existing") { pickingMode = PickingMode.Ignore };
            overwriteLabel.AddToClassList("map-editor-popover-label");
            overwriteLabel.style.marginTop = 8;
            container.Add(overwriteLabel);

            VisualElement listRoot = new() { pickingMode = PickingMode.Position };
            listRoot.name = "save-overwrite-list";
            container.Add(listRoot);

            Button saveBtn = new(() =>
            {
                string name = _saveNameField.value?.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                    SaveAsRequested?.Invoke(name);
            })
            { text = "Save Map" };
            saveBtn.AddToClassList("map-editor-action-btn");
            saveBtn.AddToClassList("map-editor-action-btn--primary");
            saveBtn.style.marginTop = 10;
            container.Add(saveBtn);
        }

        public void PopulateMapList(IReadOnlyList<MapEditorMapEntry> maps)
        {
            if (_activePopover == null)
                return;

            if (_vm.OpenPopover == "maps")
                PopulateLoadList(maps);
            else if (_vm.OpenPopover == "saveMenu")
                PopulateOverwriteList(maps);
        }

        private void PopulateLoadList(IReadOnlyList<MapEditorMapEntry> maps)
        {
            VisualElement window = _activePopover.Q(className: "map-editor-window");
            if (window == null)
                return;

            List<VisualElement> toRemove = new();
            foreach (VisualElement child in window.Children())
            {
                if (child.ClassListContains("map-editor-map-row"))
                    toRemove.Add(child);
            }

            foreach (VisualElement child in toRemove)
                child.RemoveFromHierarchy();

            int insertIndex = 1;
            foreach (MapEditorMapEntry map in maps)
            {
                Button row = new(() => LoadMapRequested?.Invoke(map.Name));
                row.AddToClassList("map-editor-map-row");
                Label name = new(map.DisplayLabel) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("map-editor-map-name");

                Button delete = new(() => DeleteMapRequested?.Invoke(map.Name)) { text = "Del" };
                delete.AddToClassList("map-editor-action-btn");
                delete.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

                VisualElement actions = new() { pickingMode = PickingMode.Position };
                actions.style.flexDirection = FlexDirection.Row;
                actions.style.alignItems = Align.Center;
                Label badge = new("Load") { pickingMode = PickingMode.Ignore };
                badge.AddToClassList("map-editor-map-badge");
                actions.Add(badge);
                actions.Add(delete);

                row.Add(name);
                row.Add(actions);
                window.Insert(insertIndex++, row);
            }
        }

        private void PopulateOverwriteList(IReadOnlyList<MapEditorMapEntry> maps)
        {
            VisualElement listRoot = _activePopover.Q(name: "save-overwrite-list");
            if (listRoot == null)
                return;

            listRoot.Clear();
            string current = _saveNameField?.value?.Trim();

            foreach (MapEditorMapEntry map in maps)
            {
                bool isTarget = string.Equals(map.Name, current, StringComparison.OrdinalIgnoreCase);
                Button row = new(() =>
                {
                    if (_saveNameField != null)
                        _saveNameField.value = map.Name;
                    _vm.SaveMapName = map.Name;
                });
                row.AddToClassList("map-editor-map-row");
                Label name = new(map.DisplayLabel) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("map-editor-map-name");
                name.EnableInClassList("map-editor-map-name--overwrite-target", isTarget);
                Label badge = new(isTarget ? "Overwriting" : "Overwrite") { pickingMode = PickingMode.Ignore };
                badge.AddToClassList("map-editor-map-badge");
                badge.EnableInClassList("map-editor-map-badge--warning", isTarget);
                row.Add(name);
                row.Add(badge);
                listRoot.Add(row);
            }
        }

        private void AddToolButton(VisualElement parent, MapEditorTool tool, string tooltip)
        {
            Button btn = CreateIconButton(_icons?.GetToolIcon(tool), tooltip, () => ToolSelected?.Invoke(tool));
            _toolButtons[tool] = btn;
            parent.Add(btn);
        }

        private Button CreateModeTab(MapEditorMode mode)
        {
            Button tab = new(() => ModeSelected?.Invoke(mode));
            tab.AddToClassList("map-editor-mode-tab");
            tab.userData = mode;

            VisualElement surface = new();
            surface.AddToClassList("map-editor-mode-tab__surface");
            surface.pickingMode = PickingMode.Ignore;

            VisualElement icon = CreateIconElement(_icons?.GetModeIcon(mode));
            icon.AddToClassList("map-editor-mode-tab__icon");
            Label label = new(MapEditorCatalog.GetModeLabel(mode).ToUpperInvariant());
            label.AddToClassList("map-editor-mode-tab__label");
            label.pickingMode = PickingMode.Ignore;
            surface.Add(icon);
            surface.Add(label);
            tab.Add(surface);
            return tab;
        }

        private Button CreateSubcategoryTab(MapEditorSubcategory subcategory)
        {
            Button tab = new(() => SubcategorySelected?.Invoke(subcategory));
            tab.AddToClassList("map-editor-subcat-tab");
            tab.EnableInClassList("map-editor-subcat-tab--active", subcategory == _vm.CurrentSubcategory);

            VisualElement surface = new();
            surface.AddToClassList("map-editor-subcat-tab__surface");
            surface.pickingMode = PickingMode.Ignore;

            VisualElement icon = CreateIconElement(_icons?.GetSubcategoryIcon(subcategory));
            icon.AddToClassList("map-editor-subcat-tab__icon");
            Label label = new(MapEditorCatalog.GetSubcategoryLabel(subcategory).ToUpperInvariant());
            label.AddToClassList("map-editor-subcat-tab__label");
            label.pickingMode = PickingMode.Ignore;
            surface.Add(icon);
            surface.Add(label);
            tab.Add(surface);
            return tab;
        }

        private static VisualElement CreateRegion(string className)
        {
            // Ignore — a Position region (esp. the full-width bottom dock) stole world picks for
            // the entire panel footprint, so vertical mouse motion / camera framing over that band
            // froze drag placement. Interactive children (buttons, windows, slots) still pick.
            VisualElement region = new() { pickingMode = PickingMode.Ignore };
            region.AddToClassList("map-editor-region");
            region.AddToClassList(className);
            return region;
        }

        private static VisualElement CreateToolbarStrip(bool vertical = false)
        {
            // Ignore the strip chrome for world picks; only buttons/fields should block placement.
            VisualElement strip = new() { pickingMode = PickingMode.Ignore };
            strip.AddToClassList("map-editor-toolbar-strip");
            strip.EnableInClassList("map-editor-toolbar-strip--vertical", vertical);
            strip.style.flexDirection = vertical ? FlexDirection.Column : FlexDirection.Row;
            return strip;
        }

        private static Button CreateIconButton(VectorImage icon, string tooltip, Action onClick)
        {
            Button btn = new(onClick);
            btn.AddToClassList("map-editor-icon-btn");
            btn.tooltip = tooltip;

            VisualElement iconElement = CreateIconElement(icon);
            iconElement.AddToClassList("map-editor-icon-btn__icon");
            btn.Add(iconElement);
            return btn;
        }

        private static Button CreateHeaderIconButton(VectorImage icon, string tooltip, Action onClick)
        {
            Button btn = new(onClick);
            btn.AddToClassList("map-editor-header-icon-btn");
            btn.tooltip = tooltip;

            VisualElement iconElement = CreateIconElement(icon);
            iconElement.AddToClassList("map-editor-header-icon-btn__icon");
            btn.Add(iconElement);
            return btn;
        }

        private static VisualElement CreateIconElement(VectorImage icon)
        {
            VisualElement element = new() { pickingMode = PickingMode.Ignore };
            element.AddToClassList("map-editor-vector-icon");
            if (icon != null)
                element.style.backgroundImage = new StyleBackground(icon);
            return element;
        }

        private static void AddSeparator(VisualElement parent, bool vertical = false)
        {
            VisualElement sep = new() { pickingMode = PickingMode.Ignore };
            sep.AddToClassList("map-editor-separator");
            sep.EnableInClassList("map-editor-separator--vertical", vertical);
            parent.Add(sep);
        }

        private static VisualElement CreateWindow(string title)
        {
            VisualElement window = new() { pickingMode = PickingMode.Position };
            window.AddToClassList("map-editor-window");
            if (!string.IsNullOrEmpty(title))
            {
                Label titleLabel = new(title) { pickingMode = PickingMode.Ignore };
                titleLabel.AddToClassList("map-editor-window__title");
                window.Add(titleLabel);
            }

            return window;
        }

        private static void AddToggleRow(VisualElement parent, string label, bool value, Action<bool> onChanged)
        {
            Toggle toggle = new(label) { value = value };
            toggle.AddToClassList("map-editor-toggle-row");
            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            parent.Add(toggle);
        }

        private void RaiseCameraSettingsChanged() =>
            CameraSettingsChanged?.Invoke(_vm.CameraFov, _vm.CameraZoomSpeed, _vm.CameraRotationSpeed);

        private static void AddSliderRow(VisualElement parent, string label, float value, float min, float max,
            string unit, Action<float> onChanged)
        {
            VisualElement row = new();
            row.AddToClassList("map-editor-slider-row");

            VisualElement header = new();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;

            Label labelElement = new(label);
            labelElement.AddToClassList("map-editor-slider-row__label");
            Label valueElement = new(FormatSliderValue(value, unit));
            valueElement.AddToClassList("map-editor-slider-row__value");

            header.Add(labelElement);
            header.Add(valueElement);

            Slider slider = new(min, max) { value = value };
            slider.AddToClassList("map-editor-slider-row__slider");
            slider.RegisterValueChangedCallback(evt =>
            {
                valueElement.text = FormatSliderValue(evt.newValue, unit);
                onChanged(evt.newValue);
            });

            row.Add(header);
            row.Add(slider);
            parent.Add(row);
        }

        private static string FormatSliderValue(float value, string unit) => $"{Mathf.RoundToInt(value)}{unit}";
    }
}
