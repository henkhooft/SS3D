using Coimbra.Services.Events;
using Coimbra.Services.PlayerLoopEvents;
using FishNet.Connection;
using FishNet.Object;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Data.AssetDatabases;
using SS3D.Logging;
using SS3D.Systems.Area;
using SS3D.Systems.Inputs;
using SS3D.Systems.Screens;
using SS3D.Systems.Tile.MapEditor.Commands;
using SS3D.Systems.Tile.FloorVisuals;
using SS3D.Systems.Tile.MapEditor.Persistence;
using SS3D.Systems.Tile.MapEditor.UI;
using SS3D.Systems.Tile.SpawnPoints;
using SS3D.Systems.Tile.TileMapCreator;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using InputSubSystem = SS3D.Systems.Inputs.InputSubSystem;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Full-screen in-game map editor replacing the legacy TileMap Creator UI.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MapEditorSubSystem : NetworkSubSystem, IMapEditorHost
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private StyleSheet _styleSheet;
        [SerializeField] private MapEditorCatalogSo _catalogAsset;
        [SerializeField] private MapEditorIconsSo _icons;
        [SerializeField] private ConstructionHologramManager _hologramManager;

        private readonly MapEditorViewModel _viewModel = new();
        private MapEditorGameplayHud _gameplayHud;
        private readonly MapEditorCatalog _catalog = new();
        private readonly MapEditorSession _session = new();
        private readonly MapEditorCommandService _commandService = new();
        private readonly MapEditorLighting _lighting = new();

        private MapEditorView _view;
        private IMapEditorPersistence _persistence;
        private InputSubSystem _inputSystem;
        private Controls.TileCreatorActions _controls;
        private TileSubSystem _tileSystem;
        private CameraFollow _cameraFollow;
        private Camera _playerCamera;
        private IInputHandle _mapEditorHandle;
        private VisualElement _overlayRoot;
        private bool _active;
        private bool _mouseOverUI;
        private bool _documentRegistered;
        private float _toastTimer;
        private Vector3? _moveSource;
        private string _moveAssetName;
        private Direction _moveDirection;
        private bool _moveIsItem;
        /// <summary>Last map name successfully saved or loaded this session — target for Ctrl+S.</summary>
        private string _lastQuickSaveName;

        /// <summary>Fired after the editor opens (Main HUD listens to suppress chrome).</summary>
        public event Action EditorOpened;

        /// <summary>Fired after the editor closes.</summary>
        public event Action EditorClosed;

        public bool IsActive => _active;

        /// <summary>
        /// Live pointer-over-UI via <see cref="InputInterface"/> (registered UITK + uGUI).
        /// Cached <see cref="_mouseOverUI"/> is only for the CSS hover class.
        /// </summary>
        public bool MouseOverUI => _active && InputInterface.IsPointerOverInterface();

        public bool IsOrbiting => _session.IsOrbiting;

        /// <summary>
        /// True for the dedicated Delete tool (always erases, no selected asset), or for the
        /// legacy Edit-tool-plus-Eraser-catalog-entry combination.
        /// </summary>
        public bool IsDeleting => _viewModel.CurrentTool == MapEditorTool.Delete ||
            (_viewModel.IsEraserSelected && _viewModel.CurrentTool == MapEditorTool.Edit);
        public MapEditorTool CurrentTool => _viewModel.CurrentTool;
        public MapEditorMode CurrentMode => _viewModel.CurrentMode;
        public MapEditorSubcategory CurrentSubcategory => _viewModel.CurrentSubcategory;
        public bool GridSnapEnabled => _viewModel.GridSnap;

        /// <summary>Client-local toast (placement blocked, delete miss, etc.).</summary>
        public void ShowLocalToast(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            _viewModel.ShowToast(message);
            _toastTimer = 1.6f;
        }

        public void SetPlacementHint(string hint)
        {
            if (_viewModel.SelectedObjectHint == hint)
                return;

            _viewModel.SelectedObjectHint = hint;
            _viewModel.NotifyChanged();
        }
        public Camera PickCamera => _playerCamera != null ? _playerCamera : Camera.main;

        protected override void OnAwake()
        {
            base.OnAwake();
            if (_document == null)
                _document = GetComponent<UIDocument>();
        }

        protected override void OnStart()
        {
            base.OnStart();
            _inputSystem = SubSystems.Get<InputSubSystem>();
            _tileSystem = SubSystems.Get<TileSubSystem>();
            _controls = _inputSystem.Inputs.TileCreator;
            _persistence = new MapEditorLocalPersistence(_tileSystem);
            _gameplayHud = new MapEditorGameplayHud(transform);

            // TileCreator.ToggleMenu is always enabled by the Global context.
            _controls.ToggleMenu.performed += HandleToggleMenu;
            EnsureCommandServiceBound();

            ResolvePlayerCamera();
        }

        private void ResolvePlayerCamera()
        {
            if (SubSystems.TryGet(out CameraSubSystem cameraSystem) && cameraSystem.PlayerCamera != null)
                _playerCamera = cameraSystem.PlayerCamera.GetComponent<Camera>();

            if (_playerCamera == null)
                _playerCamera = Camera.main;

            if (_playerCamera != null)
                _cameraFollow = _playerCamera.GetComponent<CameraFollow>();
        }

        private void EnsureCommandServiceBound()
        {
            if (!IsServer || _tileSystem?.CurrentMap == null || _tileSystem.Loader == null)
                return;

            _commandService.Bind(
                _tileSystem.CurrentMap,
                _tileSystem.Loader,
                new ConstructionService(_tileSystem.CurrentMap, _tileSystem.QueryService),
                () => _tileSystem.SyncFloorDecalsToClients(),
                _tileSystem.SpawnPoints,
                () => SpawnPointEditorView.RefreshAll());
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();
            AddHandle(UpdateEvent.AddListener(HandleUpdate));
        }

        protected override void OnDestroyed()
        {
            _controls.ToggleMenu.performed -= HandleToggleMenu;
            SetGameplayInputBlocked(false);
            _lighting.Restore();
            _gameplayHud?.SetVisible(true);
            _mapEditorHandle?.Dispose();
            _mapEditorHandle = null;
            TeardownView();
            UnregisterDocument();
            if (_document != null)
                _document.enabled = false;
            if (_active)
            {
                _active = false;
                EditorClosed?.Invoke();
            }

            base.OnDestroyed();
        }

        private void BuildView()
        {
            if (_document == null || _document.rootVisualElement == null)
                return;

            _document.rootVisualElement.pickingMode = PickingMode.Ignore;

            _overlayRoot = _document.rootVisualElement.Q<VisualElement>("overlay-root") ??
                           _document.rootVisualElement;
            _overlayRoot.pickingMode = PickingMode.Ignore;

            _view = new MapEditorView(_styleSheet, _icons, _viewModel, _catalog, _tileSystem.Loader);
            _view.Build(_overlayRoot);
            WireViewEvents();
            RebuildCatalog();
            RegisterDocument();
        }

        private void RegisterDocument()
        {
            if (_documentRegistered || _document == null)
                return;

            InputInterface.RegisterDocument(_document);
            _documentRegistered = true;
        }

        private void UnregisterDocument()
        {
            if (!_documentRegistered || _document == null)
                return;

            InputInterface.UnregisterDocument(_document);
            _documentRegistered = false;
        }

        private void WireViewEvents()
        {
            _view.ToolSelected += OnToolSelected;
            _view.UndoRequested += OnUndoRequested;
            _view.RedoRequested += OnRedoRequested;
            _view.SaveAsRequested += OnSaveAsRequested;
            _view.LoadMapRequested += OnLoadMapRequested;
            _view.DeleteMapRequested += OnDeleteMapRequested;
            _view.NewMapRequested += OnNewMapRequested;
            _view.MapListRefreshRequested += RefreshMapList;
            _view.ResetViewRequested += OnResetViewRequested;
            _view.ShowUIRequested += OnShowUiRequested;
            _view.GridSnapChanged += OnGridSnapChanged;
            _view.DebugOverlayChanged += OnDebugOverlayChanged;
            _view.CameraSettingsChanged += OnCameraSettingsChanged;
            _view.CameraRotateRequested += OnCameraRotateRequested;
            _view.CameraZoomRequested += OnCameraZoomRequested;
            _view.LayerCategoryVisibilityChanged += OnLayerCategoryVisibilityChanged;
            _view.ModeSelected += OnModeSelected;
            _view.SubcategorySelected += OnSubcategorySelected;
            _view.SearchChanged += OnSearchChanged;
            _view.AssetSelected += HandleAssetSelected;
        }

        private void RebuildCatalog()
        {
            if (_tileSystem?.Loader == null)
                return;

            _catalog.Build(_tileSystem.Loader.Assets, _catalogAsset);
            _viewModel.NotifyChanged();
        }

        private void HandleToggleMenu(InputAction.CallbackContext context)
        {
            if (_active)
                SetActive(false);
            else
                SetActive(true);
        }

        private void SetActive(bool active)
        {
            _active = active;

            if (active)
            {
                _viewModel.HideUI = false;
                EnableDocument();
                RebuildCatalog();
                EnsureCommandServiceBound();
                _viewModel.EnsureLayerDefaults();
                MapEditorLayerVisibility.Activate();
                MapEditorLayerVisibility.Apply(_viewModel);

                _mapEditorHandle ??= _inputSystem.PushContext(InputContext.MapEditor);

                // Stop CameraFollow BEFORE taking the camera — its UpdateEvent still fires
                // while disabled unless it early-outs, and it was overwriting orbit every frame.
                SetGameplayInputBlocked(true);

                if (_playerCamera == null)
                    ResolvePlayerCamera();

                Vector3 entry = _playerCamera != null ? _playerCamera.transform.position : Vector3.zero;
                if (_session.IsActive)
                    _session.Exit();
                _session.Enter(_playerCamera, entry);

                _hologramManager.enabled = true;
                _viewModel.SetTool(MapEditorTool.Edit);
                OnCameraSettingsChanged(_viewModel.CameraFov, _viewModel.CameraZoomSpeed, _viewModel.CameraRotationSpeed);
                _lighting.Apply();
                SetMouseOverUI(false);
                _gameplayHud.SetVisible(false);
                SpawnPointEditorView.EnsureExists().SetEditorOpen(true);
                EditorOpened?.Invoke();
                RpcRequestUndoState(LocalConnection);
            }
            else
            {
                _gameplayHud.SetVisible(true);
                SpawnPointEditorView.EnsureExists().SetEditorOpen(false);
                _hologramManager.DestroyHolograms();
                _hologramManager.enabled = false;
                _session.Exit();
                _lighting.Restore();
                MapEditorLayerVisibility.Deactivate();
                SetGameplayInputBlocked(false);
                _mapEditorHandle?.Dispose();
                _mapEditorHandle = null;
                ShutdownDocument();
                EditorClosed?.Invoke();
            }
        }

        /// <summary>
        /// Disables the normal follow camera while the Map Editor's own free-fly session is active.
        /// Movement/Camera/Hotkeys action maps are masked separately by <see cref="InputContext.MapEditor"/>.
        /// </summary>
        private void SetGameplayInputBlocked(bool blocked)
        {
            if (_cameraFollow != null)
                _cameraFollow.enabled = !blocked;
        }

        private void HandleAssetSelected(MapEditorCatalogEntry entry, GenericObjectSo asset)
        {
            if (entry == null)
                return;

            _viewModel.SelectEntry(entry, asset);

            if (entry.IsEraser)
            {
                _hologramManager.EnterDeleteMode();
                return;
            }

            if (MapEditorFloorDecalCatalog.TryDecode(entry.AssetName, out ushort decalId) &&
                FloorDecalCatalog.Get()?.TryGet(decalId, out FloorDecalDefinition definition) == true &&
                _viewModel.CurrentTool == MapEditorTool.Edit)
            {
                _hologramManager.SetSelectedFloorDecal(definition);
                return;
            }

            if (MapEditorSpawnCatalog.IsSpawnKey(entry.AssetName) &&
                _viewModel.CurrentTool == MapEditorTool.Edit)
            {
                _hologramManager.SetSelectedSpawnPoint(entry.AssetName);
                return;
            }

            if (asset != null && _viewModel.CurrentTool == MapEditorTool.Edit)
                _hologramManager.SetSelectedObject(asset);
        }

        private void HandleUpdate(ref EventContext context, in UpdateEvent updateEvent)
        {
            if (!_active)
                return;

            if (_session.IsActive)
                _session.Update(updateEvent.DeltaTime);

            // While orbiting, skip UI hover / tool clicks — placement picks are frozen too.
            if (!_session.IsOrbiting)
            {
                UpdateMouseOverUI();

                if (_viewModel.CurrentTool == MapEditorTool.Select && _controls.Place.WasPerformedThisFrame() && !MouseOverUI)
                    HandleSelectClick();

                if (_viewModel.CurrentTool == MapEditorTool.Dropper && _controls.Place.WasPerformedThisFrame() && !MouseOverUI)
                    HandleDropperClick();

                if (_viewModel.CurrentTool == MapEditorTool.Move && !MouseOverUI)
                    HandleMoveInput();
            }
            else
            {
                SetMouseOverUI(false);
            }
            if (Keyboard.current != null)
            {
                if (Keyboard.current.f7Key.wasPressedThisFrame)
                {
                    _viewModel.HideUI = !_viewModel.HideUI;
                    _viewModel.NotifyChanged();
                }

                if (!InputInterface.IsCapturingText)
                {
                    HandleToolHotkeys();

                    if (IsChordModifierPressed(Keyboard.current) && Keyboard.current.zKey.wasPressedThisFrame)
                        RpcUndo(LocalConnection);
                    if (IsChordModifierPressed(Keyboard.current) && Keyboard.current.yKey.wasPressedThisFrame)
                        RpcRedo(LocalConnection);
                }

                // File shortcuts work even while a search/save field is focused.
                // Ctrl+Shift+O — Unity Editor steals plain Ctrl+O for File/Open Scene (read-only profile).
                if (IsChordModifierPressed(Keyboard.current)
                    && Keyboard.current.shiftKey.isPressed
                    && Keyboard.current.oKey.wasPressedThisFrame)
                    HandleOpenMapHotkey();
                if (IsChordModifierPressed(Keyboard.current)
                    && !Keyboard.current.shiftKey.isPressed
                    && Keyboard.current.sKey.wasPressedThisFrame)
                    HandleQuickSaveHotkey();
            }

            if (_toastTimer > 0f)
            {
                _toastTimer -= updateEvent.DeltaTime;
                if (_toastTimer <= 0f)
                    _viewModel.ClearToast();
            }

            // Do NOT RefreshMapList every frame — PopulateLoadList destroys/recreates Load/Del
            // buttons, so pointer-down never meets pointer-up on the same element (New Map
            // worked because it is built once in BuildMapsPopover). Refresh on open / after save/delete.
        }

        private void RefreshMapList() => _view?.PopulateMapList(_persistence.ListMaps());

        private void HandleSelectClick()
        {
            if (!MapEditorCursorPick.TryPick(PickCamera, _tileSystem.CurrentMap, _viewModel,
                    out PlacedTileObject placed, out PlacedItemObject item))
                return;

            if (placed != null)
            {
                GenericObjectSo asset = _tileSystem.GetAsset(placed.NameString);
                _viewModel.SelectEntry(new MapEditorCatalogEntry { AssetName = placed.NameString }, asset);
                _viewModel.ShowToast($"Selected {placed.NameString} on {placed.Layer}");
                _toastTimer = 1.6f;
                return;
            }

            GenericObjectSo itemAsset = _tileSystem.GetAsset(item.NameString);
            _viewModel.SelectEntry(new MapEditorCatalogEntry { AssetName = item.NameString }, itemAsset);
            _viewModel.ShowToast($"Selected item {item.NameString}");
            _toastTimer = 1.6f;
        }

        /// <summary>
        /// Copies whatever is under the cursor into the active library selection and switches to
        /// Edit so the user can immediately place more of it.
        /// </summary>
        private void HandleDropperClick()
        {
            if (!MapEditorCursorPick.TryPick(PickCamera, _tileSystem.CurrentMap, _viewModel,
                    out PlacedTileObject placed, out PlacedItemObject item))
                return;

            CopyIntoActiveSelection(placed != null ? placed.NameString : item.NameString);
        }

        private void CopyIntoActiveSelection(string assetName)
        {
            GenericObjectSo asset = _tileSystem.GetAsset(assetName);
            _viewModel.SelectEntry(new MapEditorCatalogEntry { AssetName = assetName }, asset);
            _viewModel.SetTool(MapEditorTool.Edit);
            if (asset != null)
                _hologramManager.SetSelectedObject(asset);
            _viewModel.ShowToast($"Copied {assetName}");
            _toastTimer = 1.6f;
        }

        private void HandleMoveInput()
        {
            if (_controls.Place.WasPerformedThisFrame())
            {
                if (_moveSource == null)
                    TryBeginMove();
                else
                    TryCompleteMove();
            }
        }

        private void TryBeginMove()
        {
            if (!MapEditorCursorPick.TryPick(PickCamera, _tileSystem.CurrentMap, _viewModel,
                    out PlacedTileObject placed, out PlacedItemObject item))
                return;

            if (placed != null)
            {
                _moveSource = new Vector3(placed.WorldOrigin.x, 0f, placed.WorldOrigin.y);
                _moveAssetName = placed.NameString;
                _moveDirection = placed.Direction;
                _moveIsItem = false;
                _viewModel.ShowToast($"Moving {placed.NameString} — click destination");
                _toastTimer = 2f;
                return;
            }

            _moveSource = item.transform.position;
            _moveAssetName = item.NameString;
            _moveDirection = Direction.North;
            _moveIsItem = true;
            _viewModel.ShowToast($"Moving item {item.NameString} — click destination");
            _toastTimer = 2f;
        }

        private void TryCompleteMove()
        {
            if (_moveSource == null)
                return;

            Vector3 destination = TileHelper.GetPointedPosition(!_moveIsItem, PickCamera);
            RpcMoveObject(_moveAssetName, _moveSource.Value, destination, _moveDirection, _moveIsItem, LocalConnection);
            _moveSource = null;
            _moveAssetName = null;
        }

        private void UpdateMouseOverUI()
        {
            bool over = InputInterface.IsPointerOverInterface();
            if (over != _mouseOverUI)
                SetMouseOverUI(over);
        }

        private void SetMouseOverUI(bool over)
        {
            _mouseOverUI = over;
            _view?.SetMouseOverUI(over);
        }

        private void EnableDocument()
        {
            if (_document == null)
                return;

            if (!_document.enabled)
                _document.enabled = true;

            if (_view == null || _view.Root?.panel == null)
            {
                TeardownView();
                BuildView();
            }

            if (_view?.Root != null)
                _view.Root.style.display = DisplayStyle.Flex;

            RegisterDocument();
            _viewModel.NotifyChanged();
        }

        private void ShutdownDocument()
        {
            if (_view?.Root != null)
                _view.Root.style.display = DisplayStyle.None;
        }

        private void TeardownView()
        {
            if (_view == null)
                return;

            UnwireViewEvents();
            _view.Destroy();
            _view = null;
        }

        private void UnwireViewEvents()
        {
            if (_view == null)
                return;

            _view.ToolSelected -= OnToolSelected;
            _view.UndoRequested -= OnUndoRequested;
            _view.RedoRequested -= OnRedoRequested;
            _view.SaveAsRequested -= OnSaveAsRequested;
            _view.LoadMapRequested -= OnLoadMapRequested;
            _view.DeleteMapRequested -= OnDeleteMapRequested;
            _view.NewMapRequested -= OnNewMapRequested;
            _view.MapListRefreshRequested -= RefreshMapList;
            _view.ResetViewRequested -= OnResetViewRequested;
            _view.ShowUIRequested -= OnShowUiRequested;
            _view.GridSnapChanged -= OnGridSnapChanged;
            _view.DebugOverlayChanged -= OnDebugOverlayChanged;
            _view.CameraSettingsChanged -= OnCameraSettingsChanged;
            _view.CameraRotateRequested -= OnCameraRotateRequested;
            _view.CameraZoomRequested -= OnCameraZoomRequested;
            _view.LayerCategoryVisibilityChanged -= OnLayerCategoryVisibilityChanged;
            _view.ModeSelected -= OnModeSelected;
            _view.SubcategorySelected -= OnSubcategorySelected;
            _view.SearchChanged -= OnSearchChanged;
            _view.AssetSelected -= HandleAssetSelected;
        }

        private void OnToolSelected(MapEditorTool tool)
        {
            _viewModel.SetTool(tool);

            switch (tool)
            {
                case MapEditorTool.Delete:
                    _hologramManager.EnterDeleteMode();
                    break;
                case MapEditorTool.Edit:
                    RestoreConstructHologramFromSelection();
                    break;
                default:
                    // Select / Dropper / Move — placement ghosts must not linger.
                    _hologramManager.ClearSelection();
                    break;
            }
        }

        private void RestoreConstructHologramFromSelection()
        {
            MapEditorCatalogEntry entry = _viewModel.SelectedEntry;
            if (entry == null)
            {
                _hologramManager.ClearSelection();
                return;
            }

            HandleAssetSelected(entry, _viewModel.SelectedAsset);
        }

        private void HandleToolHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || IsChordModifierPressed(keyboard) || keyboard.altKey.isPressed)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                OnToolSelected(MapEditorTool.Edit);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                OnToolSelected(MapEditorTool.Select);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                OnToolSelected(MapEditorTool.Dropper);
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
                OnToolSelected(MapEditorTool.Delete);
        }

        private static bool IsChordModifierPressed(Keyboard keyboard) =>
            keyboard != null
            && (keyboard.ctrlKey.isPressed
                || keyboard.leftCommandKey.isPressed
                || keyboard.rightCommandKey.isPressed);

        private void HandleOpenMapHotkey()
        {
            if (!_active || _view == null)
                return;

            _view.OpenPopover("maps");
            RefreshMapList();
        }

        private void HandleQuickSaveHotkey()
        {
            if (!_active)
                return;

            string name = ResolveQuickSaveName();
            if (string.IsNullOrWhiteSpace(name))
            {
                _view?.OpenPopover("saveMenu");
                RefreshMapList();
                return;
            }

            RememberQuickSaveName(name);
            RpcSaveMap(name, overwrite: true, LocalConnection);
        }

        private string ResolveQuickSaveName()
        {
            if (!string.IsNullOrWhiteSpace(_lastQuickSaveName))
                return _lastQuickSaveName.Trim();

            string typed = _viewModel.SaveMapName?.Trim();
            if (!string.IsNullOrWhiteSpace(typed) &&
                !string.Equals(typed, "Untitled Map", StringComparison.OrdinalIgnoreCase))
                return typed;

            return null;
        }

        private void RememberQuickSaveName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            _lastQuickSaveName = name.Trim();
            _viewModel.SaveMapName = _lastQuickSaveName;
        }

        private void OnUndoRequested() => RpcUndo(LocalConnection);

        private void OnRedoRequested() => RpcRedo(LocalConnection);

        // The Save Map popover always shows an "Overwriting" warning inline when the typed name
        // collides with an existing save, so the Save button itself can safely always overwrite.
        private void OnSaveAsRequested(string name)
        {
            RememberQuickSaveName(name);
            RpcSaveMap(name, true, LocalConnection);
        }

        private void OnLoadMapRequested(string name)
        {
            RememberQuickSaveName(name);
            // Immediate local feedback — TargetToast only runs if the ServerRpc auth path succeeds.
            _viewModel.ShowToast($"Loading {name}…");
            _toastTimer = 1.6f;

            if (IsServer)
            {
                ApplyLoadMap(name, LocalConnection);
                return;
            }

            RpcLoadMap(name, LocalConnection);
        }

        private void ApplyLoadMap(string mapName, NetworkConnection conn)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
            {
                TargetToast(conn, "Not authorized to load maps.");
                return;
            }

            Log.Information(this, "Map editor loading template {mapName}", Logs.Important, mapName);
            _persistence.Load(mapName);
            _commandService.ClearHistory();
            TargetSyncUndoState(conn, 0, 0);
            TargetToast(conn, $"Loaded {mapName}.");
        }

        private void OnDeleteMapRequested(string name) => RpcDeleteMap(name, LocalConnection);

        private void OnNewMapRequested()
        {
            _lastQuickSaveName = null;
            _viewModel.SaveMapName = "Untitled Map";
            RpcNewMap(LocalConnection);
        }

        private void OnResetViewRequested() => _session.ResetPosition();

        private void OnCameraRotateRequested(int direction) => _session.RotateStep(15f * direction);

        private void OnCameraZoomRequested(int direction) => _session.ZoomStep(2f * direction);

        private void OnShowUiRequested()
        {
            _viewModel.HideUI = false;
            _viewModel.NotifyChanged();
        }

        private void OnGridSnapChanged(bool value) => _viewModel.GridSnap = value;

        private void OnCameraSettingsChanged(float fov, float zoomSpeed, float rotationSpeed)
        {
            Camera cam = PickCamera;
            if (cam != null)
                cam.fieldOfView = fov;

            _session?.SetSpeeds(zoomSpeed, rotationSpeed);

            // Sliders are shown on a 1-10 scale; 5 is the neutral (1x) speed multiplier.
            // CameraFollow is disabled while the map editor is open; keep multipliers in sync
            // for when the editor closes.
            if (_cameraFollow != null)
            {
                _cameraFollow.ZoomSpeedMultiplier = zoomSpeed / 5f;
                _cameraFollow.RotationSpeedMultiplier = rotationSpeed / 5f;
            }
        }

        private void OnDebugOverlayChanged(bool value)
        {
            _viewModel.DebugOverlay = value;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AreaDevSettings.ShowAreaGizmos = value;
#endif
        }

        private void OnLayerCategoryVisibilityChanged(TileLayerCategory category, bool visible)
        {
            _viewModel.SetLayerCategoryVisible(category, visible);
            MapEditorLayerVisibility.Apply(_viewModel);
        }

        private void OnModeSelected(MapEditorMode mode)
        {
            _viewModel.SetMode(mode);
            SyncHologramToLibrarySelection();
        }

        private void OnSubcategorySelected(MapEditorSubcategory sub)
        {
            _viewModel.SetSubcategory(sub);
            SyncHologramToLibrarySelection();
        }

        /// <summary>
        /// Subcategory/mode tabs should update the ghost immediately — delete used to keep the
        /// previous prefab until the cursor hovered a matching tile (wall mounts especially).
        /// </summary>
        private void SyncHologramToLibrarySelection()
        {
            if (IsDeleting)
            {
                _hologramManager.RefreshDeletePreview();
                return;
            }

            if (CurrentTool != MapEditorTool.Edit)
                return;

            TrySelectFirstAssetInCurrentSubcategory();
        }

        /// <summary>
        /// First placeable catalog entry for the active subcategory (skips Eraser / floor-decal slots).
        /// Used as the Delete-tool ghost prototype when the cursor is not over a matching target.
        /// </summary>
        public bool TryGetSubcategoryPrototypeAssetName(MapEditorSubcategory subcategory, out string assetName)
        {
            assetName = null;
            if (_catalog == null)
                return false;

            if (subcategory == MapEditorSubcategory.Overlays ||
                MapEditorCatalog.IsScriptingSubcategory(subcategory) ||
                MapEditorDeleteTargeting.RequiresSubcategorySelection(subcategory) ||
                MapEditorDeleteTargeting.IsItemSubcategory(subcategory))
                return false;

            foreach (MapEditorCatalogEntry entry in _catalog.Query(_viewModel.CurrentMode, subcategory, null))
            {
                if (entry.IsEraser || MapEditorFloorDecalCatalog.TryDecode(entry.AssetName, out _))
                    continue;

                assetName = entry.AssetName;
                return !string.IsNullOrEmpty(assetName);
            }

            return false;
        }

        private void TrySelectFirstAssetInCurrentSubcategory()
        {
            MapEditorSubcategory sub = _viewModel.CurrentSubcategory;
            if (_viewModel.SelectedEntry != null &&
                !_viewModel.SelectedEntry.IsEraser &&
                _viewModel.SelectedEntry.Subcategory == sub)
            {
                // Already holding something from this tab — keep it, but refresh the ghost.
                HandleAssetSelected(_viewModel.SelectedEntry, _viewModel.SelectedAsset);
                return;
            }

            foreach (MapEditorCatalogEntry entry in _catalog.Query(_viewModel.CurrentMode, sub, _viewModel.SearchText))
            {
                if (entry.IsEraser)
                    continue;

                GenericObjectSo asset = null;
                if (MapEditorFloorDecalCatalog.TryDecode(entry.AssetName, out _) ||
                    MapEditorSpawnCatalog.IsSpawnKey(entry.AssetName))
                {
                    HandleAssetSelected(entry, null);
                    return;
                }

                _catalog.TryGetAsset(entry.AssetName, _tileSystem.Loader, out asset);
                HandleAssetSelected(entry, asset);
                return;
            }

            _hologramManager.ClearSelection();
        }

        private void OnSearchChanged(string text)
        {
            _viewModel.SearchText = text;
            _viewModel.NotifyChanged();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcSaveMap(string mapName, bool overwrite, NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            _persistence.Save(mapName, overwrite);
            TargetToast(conn, $"Saved {mapName}.");
            TargetRefreshMapList(conn);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcLoadMap(string mapName, NetworkConnection conn = null)
        {
            ApplyLoadMap(mapName, conn);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcDeleteMap(string mapName, NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            _persistence.Delete(mapName);
            TargetToast(conn, $"Deleted {mapName}.");
            TargetRefreshMapList(conn);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcNewMap(NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            if (_tileSystem?.CurrentMap == null)
                return;

            _tileSystem.CurrentMap.Clear();
            _tileSystem.ClearSpawnPoints();
            _commandService.ClearHistory();
            _hologramManager.DestroyHolograms();
            SpawnPointEditorView.RefreshAll();
            TargetSyncUndoState(conn, 0, 0);
            TargetToast(conn, "Started a new empty map.");
        }

        [TargetRpc]
        private void TargetRefreshMapList(NetworkConnection conn) => RefreshMapList();

        public void SubmitCommands(MapEditorCommandDto[] commands) =>
            RpcExecuteCommands(commands, LocalConnection);

        [ServerRpc(RequireOwnership = false)]
        private void RpcExecuteCommands(MapEditorCommandDto[] commands, NetworkConnection conn = null)
        {
            EnsureCommandServiceBound();
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            if (commands == null || commands.Length == 0 || _commandService.Context == null)
                return;

            MapEditorCommandContext ctx = _commandService.Context;
            List<IMapEditorCommand> list = new(commands.Length);
            foreach (MapEditorCommandDto dto in commands)
            {
                if (dto.Kind is MapEditorCommandKind.PlaceTile or MapEditorCommandKind.PlaceItem
                    or MapEditorCommandKind.SetFloorDecal or MapEditorCommandKind.PlaceSpawnPoint)
                {
                    MapEditorCommandFactory.TryCreatePlaceCommands(dto, ctx, list);
                    continue;
                }

                list.Add(MapEditorCommandFactory.FromDto(dto, ctx));
            }

            if (list.Count == 0)
                return;

            _commandService.ExecuteCompound(list);
            TargetSyncUndoState(conn, _commandService.UndoDepth, _commandService.RedoDepth);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcUndo(NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            _commandService.Undo();
            TargetSyncUndoState(conn, _commandService.UndoDepth, _commandService.RedoDepth);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcRedo(NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            _commandService.Redo();
            TargetSyncUndoState(conn, _commandService.UndoDepth, _commandService.RedoDepth);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcMoveObject(string assetName, Vector3 from, Vector3 to, Direction direction, bool isItem,
            NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            IMapEditorCommand command = isItem
                ? new CompoundCommand(new IMapEditorCommand[]
                {
                    new ClearItemCommand(assetName, from, direction),
                    new PlaceItemCommand(assetName, to, direction),
                })
                : new MoveTileCommand(assetName, from, to, direction);

            _commandService.Execute(command);
            TargetSyncUndoState(conn, _commandService.UndoDepth, _commandService.RedoDepth);
            TargetToast(conn, "Moved object.");
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcRequestUndoState(NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            TargetSyncUndoState(conn, _commandService.UndoDepth, _commandService.RedoDepth);
        }

        [TargetRpc]
        private void TargetSyncUndoState(NetworkConnection conn, int undoDepth, int redoDepth) =>
            _viewModel.SetUndoState(undoDepth, redoDepth);

        [TargetRpc]
        private void TargetToast(NetworkConnection conn, string message)
        {
            _viewModel.ShowToast(message);
            _toastTimer = 1.6f;
        }
    }
}
