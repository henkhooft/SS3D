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
        private readonly StyleSheet _styleSheet;
        private readonly MapEditorIconsSo _icons;
        private readonly MapEditorViewModel _vm;
        private readonly MapEditorCatalog _catalog;
        private readonly TileResourceLoader _loader;

        private VisualElement _root;
        private VisualElement _hudLayer;
        private VisualElement _toolsToolbar;
        private VisualElement _viewToolbar;
        private VisualElement _selectedPanel;
        private VisualElement _modeRail;
        private VisualElement _subcatRow;
        private ScrollView _gridScroll;
        private VisualElement _grid;
        private TextField _searchField;
        private Label _windowTitle;
        private Label _selectedName;
        private Label _selectedMeta;
        private Label _selectedHint;
        private VisualElement _selectedIcon;
        private Label _toast;
        private VisualElement _toolsPopoverAnchor;
        private VisualElement _viewPopoverAnchor;
        private VisualElement _activePopover;
        private TextField _saveAsField;

        private readonly Dictionary<MapEditorTool, Button> _toolButtons = new();
        private readonly Dictionary<MapEditorMode, Button> _modeButtons = new();
        private readonly Dictionary<MapEditorSubcategory, Button> _subcatButtons = new();

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

        public event Action ExitRequested;
        public event Action<MapEditorTool> ToolSelected;
        public event Action UndoRequested;
        public event Action RedoRequested;
        public event Action QuicksaveRequested;
        public event Action<string> SaveAsRequested;
        public event Action<string> LoadMapRequested;
        public event Action<string> DeleteMapRequested;
        public event Action ResetViewRequested;
        public event Action HideUIRequested;
        public event Action ShowUIRequested;
        public event Action<bool> GridSnapChanged;
        public event Action<bool> DebugOverlayChanged;
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

            BuildExitButton();
            BuildToolsToolbar();
            BuildViewToolbar();
            BuildSelectedPanel();
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

        public bool IsPointerOverInteractiveUI(Vector2 screenPosition)
        {
            if (_root?.panel == null)
                return false;

            VisualElement picked = _root.panel.Pick(screenPosition);
            return IsInteractivePick(picked);
        }

        private static bool IsInteractivePick(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current.pickingMode == PickingMode.Position)
                    return true;
            }

            return false;
        }

        private void BuildExitButton()
        {
            VisualElement region = CreateRegion("map-editor-region--top-left");
            Button exit = new(() => ExitRequested?.Invoke());
            exit.AddToClassList("map-editor-exit-btn");
            exit.AddToClassList("map-editor-exit-btn--labeled");
            exit.tooltip = "Exit";

            VisualElement icon = CreateIconElement(_icons?.Exit);
            icon.AddToClassList("map-editor-icon-btn__icon");
            Label label = new("Exit");
            label.AddToClassList("map-editor-exit-btn__label");
            exit.Add(icon);
            exit.Add(label);
            region.Add(exit);
            _hudLayer.Add(region);
        }

        private void BuildToolsToolbar()
        {
            VisualElement region = CreateRegion("map-editor-region--top-center");
            _toolsPopoverAnchor = region;

            _toolsToolbar = CreateToolbarStrip();
            AddToolButton(_toolsToolbar, MapEditorTool.Select, "Select");
            AddToolButton(_toolsToolbar, MapEditorTool.Edit, "Edit");
            AddToolButton(_toolsToolbar, MapEditorTool.Move, "Move");
            AddSeparator(_toolsToolbar);

            Button undo = CreateIconButton(_icons?.Undo, "Undo", () => UndoRequested?.Invoke());
            undo.name = "undo-btn";
            _toolsToolbar.Add(undo);

            Button redo = CreateIconButton(_icons?.Redo, "Redo", () => RedoRequested?.Invoke());
            redo.name = "redo-btn";
            _toolsToolbar.Add(redo);
            AddSeparator(_toolsToolbar);

            _toolsToolbar.Add(CreateIconButton(_icons?.Quicksave, "Quicksave", () => QuicksaveRequested?.Invoke()));
            _toolsToolbar.Add(CreateIconButton(_icons?.OpenMap, "Map selection", ToggleMapsPopover));

            region.Add(_toolsToolbar);
            _hudLayer.Add(region);
        }

        private void BuildViewToolbar()
        {
            VisualElement region = CreateRegion("map-editor-region--top-right");
            _viewPopoverAnchor = region;

            _viewToolbar = CreateToolbarStrip();
            _viewToolbar.Add(CreateIconButton(_icons?.ResetView, "Reset position", () => ResetViewRequested?.Invoke()));
            _viewToolbar.Add(CreateIconButton(_icons?.Layers, "Layer view mode", () => TogglePopover("layers")));
            _viewToolbar.Add(CreateIconButton(_icons?.EyeOff, "Hide UI", () => HideUIRequested?.Invoke()));
            _viewToolbar.Add(CreateIconButton(_icons?.Settings, "Map editor settings", () => TogglePopover("settings")));

            region.Add(_viewToolbar);
            _hudLayer.Add(region);
        }

        private void BuildSelectedPanel()
        {
            VisualElement region = CreateRegion("map-editor-region--selected");
            VisualElement window = CreateWindow("Selected Object");
            _selectedPanel = window;

            VisualElement row = new();
            row.AddToClassList("map-editor-selected-panel");

            _selectedIcon = new VisualElement();
            _selectedIcon.AddToClassList("map-editor-selected-icon");

            VisualElement textCol = new();
            _selectedName = new Label("—");
            _selectedName.AddToClassList("map-editor-selected-name");
            _selectedMeta = new Label("Placement hint");
            _selectedMeta.AddToClassList("map-editor-selected-meta");
            _selectedHint = new Label();
            _selectedHint.AddToClassList("map-editor-selected-hint");

            textCol.Add(_selectedName);
            textCol.Add(_selectedMeta);
            textCol.Add(_selectedHint);
            row.Add(_selectedIcon);
            row.Add(textCol);
            window.Add(row);
            region.Add(window);
            _hudLayer.Add(region);
        }

        private void BuildBottomDock()
        {
            VisualElement region = CreateRegion("map-editor-region--bottom");
            region.AddToClassList("map-editor-bottom-row");

            _modeRail = new VisualElement();
            _modeRail.AddToClassList("map-editor-mode-rail");
            foreach (MapEditorMode mode in Enum.GetValues(typeof(MapEditorMode)))
            {
                Button tab = CreateModeTab(mode);
                _modeButtons[mode] = tab;
                _modeRail.Add(tab);
            }

            VisualElement library = new();
            library.AddToClassList("map-editor-library");
            VisualElement libWindow = CreateWindow("Object Library");
            _windowTitle = libWindow.Q<Label>(className: "map-editor-window__title");

            VisualElement header = new();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;

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

            header.Add(_subcatRow);
            header.Add(search);

            _gridScroll = new ScrollView(ScrollViewMode.Vertical);
            _gridScroll.AddToClassList("map-editor-grid-scroll");
            _grid = new VisualElement();
            _grid.AddToClassList("map-editor-grid");
            _gridScroll.Add(_grid);

            libWindow.Add(header);
            libWindow.Add(_gridScroll);
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
            RebuildGrid();

            string selectedLabel = _vm.IsEraserSelected ? "Eraser" : _vm.SelectedAsset?.NameString ?? "—";
            _selectedName.text = selectedLabel;
            _selectedMeta.text =
                $"Placement hint · Rotation {(int)_vm.CurrentRotation * 90}° · Snap {(_vm.GridSnap ? "On" : "Off")}";
            _selectedHint.text = _vm.SelectedObjectHint;
            _selectedHint.EnableInClassList("map-editor-selected-hint--active",
                _vm.CurrentTool == MapEditorTool.Edit && (_vm.SelectedAsset != null || _vm.IsEraserSelected));

            if (_vm.SelectedAsset?.icon != null)
                _selectedIcon.style.backgroundImage = new StyleBackground(_vm.SelectedAsset.icon);
            else
                _selectedIcon.style.backgroundImage = null;

            Button undoBtn = _toolsToolbar?.Q<Button>("undo-btn");
            Button redoBtn = _toolsToolbar?.Q<Button>("redo-btn");
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
                _windowTitle.text =
                    $"Object Library — {MapEditorCatalog.GetModeLabel(_vm.CurrentMode)} / {MapEditorCatalog.GetSubcategoryLabel(_vm.CurrentSubcategory)}";
            }
        }

        private void RebuildGrid()
        {
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

            bool any = false;
            foreach (MapEditorCatalogEntry entry in entries)
            {
                any = true;
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
                _grid.Add(slot);
            }

            if (!any)
            {
                Label empty = new($"No matches for \"{_vm.SearchText}\".");
                empty.AddToClassList("map-editor-stub-message");
                _grid.Add(empty);
            }
        }

        private void ToggleMapsPopover() => TogglePopover("maps");

        private void TogglePopover(string key)
        {
            _vm.OpenPopover = _vm.OpenPopover == key ? null : key;
            RefreshPopover();
        }

        private void RefreshPopover()
        {
            _activePopover?.RemoveFromHierarchy();
            _activePopover = null;

            if (string.IsNullOrEmpty(_vm.OpenPopover))
                return;

            VisualElement anchor = _vm.OpenPopover == "maps" ? _toolsPopoverAnchor : _viewPopoverAnchor;
            VisualElement popover = new();
            popover.AddToClassList("map-editor-popover");
            if (_vm.OpenPopover == "maps")
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

            if (_vm.OpenPopover == "maps")
                RequestMapListRefresh();
        }

        public void RequestMapListRefresh() => MapListRefreshRequested?.Invoke();

        private static string GetPopoverTitle(string key) =>
            key switch
            {
                "maps" => "Map Selection",
                "layers" => "Layer View Mode",
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
                case "layers":
                    _vm.EnsureLayerDefaults();
                    foreach (TileLayerCategory category in TileLayerCategoryMapping.AllCategories)
                    {
                        AddToggleRow(container, TileLayerCategoryMapping.GetDisplayName(category),
                            _vm.IsLayerCategoryVisible(category),
                            v => LayerCategoryVisibilityChanged?.Invoke(category, v));
                    }

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

            _saveAsField = new TextField("Save as…") { value = string.Empty };
            _saveAsField.AddToClassList("map-editor-save-field");
            Button saveBtn = new(() =>
            {
                if (!string.IsNullOrWhiteSpace(_saveAsField.value))
                    SaveAsRequested?.Invoke(_saveAsField.value.Trim());
            })
            { text = "Save" };
            saveBtn.AddToClassList("map-editor-action-btn");
            container.Add(_saveAsField);
            container.Add(saveBtn);
        }

        public void PopulateMapList(IReadOnlyList<Persistence.MapEditorMapEntry> maps)
        {
            if (_activePopover == null || _vm.OpenPopover != "maps")
                return;

            VisualElement window = _activePopover.Q(className: "map-editor-window");
            if (window == null)
                return;

            // Remove old rows (keep title, save field, save btn, close)
            List<VisualElement> toRemove = new();
            foreach (VisualElement child in window.Children())
            {
                if (child.ClassListContains("map-editor-map-row"))
                    toRemove.Add(child);
            }

            foreach (VisualElement child in toRemove)
                child.RemoveFromHierarchy();

            int insertIndex = 2;
            foreach (Persistence.MapEditorMapEntry map in maps)
            {
                VisualElement row = new();
                row.AddToClassList("map-editor-map-row");
                Label name = new(map.DisplayLabel);
                name.AddToClassList("map-editor-map-name");
                Button load = new(() => LoadMapRequested?.Invoke(map.Name)) { text = "Load" };
                load.AddToClassList("map-editor-action-btn");
                Button delete = new(() => DeleteMapRequested?.Invoke(map.Name)) { text = "Del" };
                delete.AddToClassList("map-editor-action-btn");
                delete.style.marginLeft = 4;

                VisualElement actions = new();
                actions.style.flexDirection = FlexDirection.Row;
                actions.Add(load);
                actions.Add(delete);
                row.Add(name);
                row.Add(actions);
                window.Insert(insertIndex++, row);
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
            Label label = new(MapEditorCatalog.GetModeLabel(mode));
            label.AddToClassList("map-editor-mode-tab__label");
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
            Label label = new(MapEditorCatalog.GetSubcategoryLabel(subcategory));
            label.AddToClassList("map-editor-subcat-tab__label");
            surface.Add(icon);
            surface.Add(label);
            tab.Add(surface);
            return tab;
        }

        private static VisualElement CreateRegion(string className)
        {
            VisualElement region = new() { pickingMode = PickingMode.Position };
            region.AddToClassList("map-editor-region");
            region.AddToClassList(className);
            return region;
        }

        private static VisualElement CreateToolbarStrip()
        {
            VisualElement strip = new();
            strip.AddToClassList("map-editor-toolbar-strip");
            strip.style.flexDirection = FlexDirection.Row;
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

        private static VisualElement CreateIconElement(VectorImage icon)
        {
            VisualElement element = new() { pickingMode = PickingMode.Ignore };
            element.AddToClassList("map-editor-vector-icon");
            if (icon != null)
                element.style.backgroundImage = new StyleBackground(icon);
            return element;
        }

        private static void AddSeparator(VisualElement parent)
        {
            VisualElement sep = new();
            sep.AddToClassList("map-editor-separator");
            parent.Add(sep);
        }

        private static VisualElement CreateWindow(string title)
        {
            VisualElement window = new();
            window.AddToClassList("map-editor-window");
            Label titleLabel = new(title);
            titleLabel.AddToClassList("map-editor-window__title");
            window.Add(titleLabel);
            return window;
        }

        private static void AddToggleRow(VisualElement parent, string label, bool value, Action<bool> onChanged)
        {
            Toggle toggle = new(label) { value = value };
            toggle.AddToClassList("map-editor-toggle-row");
            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            parent.Add(toggle);
        }
    }
}
