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
using SS3D.Systems.Tile.MapEditor.Persistence;
using SS3D.Systems.Tile.MapEditor.UI;
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

        private MapEditorView _view;
        private IMapEditorPersistence _persistence;
        private InputSubSystem _inputSystem;
        private Controls.TileCreatorActions _controls;
        private TileSubSystem _tileSystem;
        private CameraFollow _cameraFollow;
        private bool _gameplayInputBlocked;
        private VisualElement _overlayRoot;
        private bool _active;
        private bool _mouseOverUI;
        private float _toastTimer;
        private Vector3? _moveSource;
        private string _moveAssetName;
        private Direction _moveDirection;
        private bool _moveIsItem;

        public bool IsActive => _active;
        public bool MouseOverUI => _mouseOverUI;
        public bool IsDeleting => _viewModel.IsEraserSelected && _viewModel.CurrentTool == MapEditorTool.Edit;
        public MapEditorTool CurrentTool => _viewModel.CurrentTool;
        public bool GridSnapEnabled => _viewModel.GridSnap;

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

            _inputSystem.ToggleAction(_controls.ToggleMenu, true);
            _controls.ToggleMenu.performed += HandleToggleMenu;

            if (IsServer && _tileSystem?.CurrentMap != null && _tileSystem.Loader != null)
                _commandService.Bind(_tileSystem.CurrentMap, _tileSystem.Loader,
                    new ConstructionService(_tileSystem.CurrentMap, _tileSystem.QueryService));

            if (Camera.main != null)
                _cameraFollow = Camera.main.GetComponent<CameraFollow>();
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
            _gameplayHud?.SetVisible(true);
            TeardownView();
            if (_document != null)
                _document.enabled = false;
            base.OnDestroyed();
        }

        private void BuildView()
        {
            if (_document == null || _document.rootVisualElement == null)
                return;

            _overlayRoot = _document.rootVisualElement.Q<VisualElement>("overlay-root") ??
                           _document.rootVisualElement;
            _overlayRoot.pickingMode = PickingMode.Ignore;

            _view = new MapEditorView(_styleSheet, _icons, _viewModel, _catalog, _tileSystem.Loader);
            _view.Build(_overlayRoot);
            WireViewEvents();
            RebuildCatalog();
        }

        private void WireViewEvents()
        {
            _view.ExitRequested += RequestExit;
            _view.ToolSelected += OnToolSelected;
            _view.UndoRequested += OnUndoRequested;
            _view.RedoRequested += OnRedoRequested;
            _view.QuicksaveRequested += HandleQuicksave;
            _view.SaveAsRequested += OnSaveAsRequested;
            _view.LoadMapRequested += OnLoadMapRequested;
            _view.DeleteMapRequested += OnDeleteMapRequested;
            _view.ResetViewRequested += OnResetViewRequested;
            _view.HideUIRequested += OnHideUiRequested;
            _view.ShowUIRequested += OnShowUiRequested;
            _view.GridSnapChanged += OnGridSnapChanged;
            _view.DebugOverlayChanged += OnDebugOverlayChanged;
            _view.LayerVisibilityChanged += OnLayerVisibilityChanged;
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
                EnableDocument();
                RebuildCatalog();
                MapEditorLayerVisibility.Activate();
                MapEditorLayerVisibility.Apply(
                    _viewModel.ShowUpperLayers,
                    _viewModel.ShowLowerLayers,
                    _viewModel.ShowPipingLayers);

                _inputSystem.ToggleActionMap(_controls, true, new[] { _controls.ToggleMenu });
                _inputSystem.ToggleCollisions(_controls, false);

                Vector3 entry = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                if (_session.IsActive)
                    _session.Exit();
                _session.Enter(Camera.main, entry);

                _hologramManager.enabled = true;
                _viewModel.SetTool(MapEditorTool.Edit);
                SetMouseOverUI(false);
                _inputSystem.ToggleAction(_controls.Place, true);
                SetGameplayInputBlocked(true);
                _gameplayHud.SetVisible(false);
                RpcRequestUndoState(LocalConnection);
            }
            else
            {
                _gameplayHud.SetVisible(true);
                _hologramManager.DestroyHolograms();
                _hologramManager.enabled = false;
                _session.Exit();
                MapEditorLayerVisibility.Deactivate();
                SetGameplayInputBlocked(false);
                _inputSystem.ToggleActionMap(_controls, false, new[] { _controls.ToggleMenu });
                _inputSystem.ToggleCollisions(_controls, true);
                ShutdownDocument();
            }
        }

        private void SetGameplayInputBlocked(bool blocked)
        {
            if (blocked && !_gameplayInputBlocked)
            {
                _inputSystem.ToggleActionMap(_inputSystem.Inputs.Movement, false);
                _inputSystem.ToggleActionMap(_inputSystem.Inputs.Camera, false);
                _inputSystem.ToggleActionMap(_inputSystem.Inputs.Hotkeys, false);
                if (_cameraFollow != null)
                    _cameraFollow.enabled = false;
                _gameplayInputBlocked = true;
            }
            else if (!blocked && _gameplayInputBlocked)
            {
                _inputSystem.ToggleActionMap(_inputSystem.Inputs.Movement, true);
                _inputSystem.ToggleActionMap(_inputSystem.Inputs.Camera, true);
                _inputSystem.ToggleActionMap(_inputSystem.Inputs.Hotkeys, true);
                if (_cameraFollow != null)
                    _cameraFollow.enabled = true;
                _gameplayInputBlocked = false;
            }
        }

        private void RequestExit()
        {
            _viewModel.ShowToast("Map editor closed.");
            SetActive(false);
        }

        private void HandleAssetSelected(MapEditorCatalogEntry entry, GenericObjectSo asset)
        {
            if (entry == null)
                return;

            _viewModel.SelectEntry(entry, asset);

            if (entry.IsEraser)
            {
                _hologramManager.ClearSelection();
                return;
            }

            if (asset != null && _viewModel.CurrentTool == MapEditorTool.Edit)
                _hologramManager.SetSelectedObject(asset);
        }

        private void HandleQuicksave()
        {
            string name = $"quicksave_{DateTime.Now:HHmmss}";
            RpcSaveMap(name, true, LocalConnection);
        }

        private void HandleUpdate(ref EventContext context, in UpdateEvent updateEvent)
        {
            if (!_active)
                return;

            if (_session.IsActive)
                _session.Update(updateEvent.DeltaTime);

            UpdateMouseOverUI();

            if (_viewModel.CurrentTool == MapEditorTool.Select && _controls.Place.WasPerformedThisFrame() && !MouseOverUI)
                HandleSelectClick();

            if (_viewModel.CurrentTool == MapEditorTool.Move && !MouseOverUI)
                HandleMoveInput();

            if (Keyboard.current != null)
            {
                if (Keyboard.current.f7Key.wasPressedThisFrame)
                {
                    _viewModel.HideUI = !_viewModel.HideUI;
                    _viewModel.NotifyChanged();
                }

                if (Keyboard.current.ctrlKey.isPressed && Keyboard.current.zKey.wasPressedThisFrame)
                    RpcUndo(LocalConnection);
                if (Keyboard.current.ctrlKey.isPressed && Keyboard.current.yKey.wasPressedThisFrame)
                    RpcRedo(LocalConnection);
            }

            if (_toastTimer > 0f)
            {
                _toastTimer -= updateEvent.DeltaTime;
                if (_toastTimer <= 0f)
                    _viewModel.ClearToast();
            }

            if (_viewModel.OpenPopover == "maps" && IsServer)
                _view?.PopulateMapList(_persistence.ListMaps());
        }

        private void HandleSelectClick()
        {
            Vector3 position = TileHelper.GetPointedPosition(true);
            TileMap map = _tileSystem.CurrentMap;
            if (map == null)
                return;

            if (map.TryGetTileLocations(position, out ITileLocation[] locations))
            {
                foreach (ITileLocation location in locations)
                {
                    foreach (PlacedTileObject placed in location.GetAllPlacedObject())
                    {
                        GenericObjectSo asset = _tileSystem.GetAsset(placed.NameString);
                        _viewModel.SelectEntry(new MapEditorCatalogEntry { AssetName = placed.NameString }, asset);
                        _viewModel.ShowToast($"Selected {placed.NameString} on {placed.Layer}");
                        _toastTimer = 1.6f;
                        return;
                    }
                }
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                PlacedItemObject item = hit.collider.GetComponentInParent<PlacedItemObject>();
                if (item != null)
                {
                    GenericObjectSo asset = _tileSystem.GetAsset(item.NameString);
                    _viewModel.SelectEntry(new MapEditorCatalogEntry { AssetName = item.NameString }, asset);
                    _viewModel.ShowToast($"Selected item {item.NameString}");
                    _toastTimer = 1.6f;
                }
            }
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
            Vector3 position = TileHelper.GetPointedPosition(true);
            TileMap map = _tileSystem.CurrentMap;
            if (map == null)
                return;

            if (map.TryGetTileLocations(position, out ITileLocation[] locations))
            {
                foreach (ITileLocation location in locations)
                {
                    foreach (PlacedTileObject placed in location.GetAllPlacedObject())
                    {
                        _moveSource = new Vector3(placed.WorldOrigin.x, 0f, placed.WorldOrigin.y);
                        _moveAssetName = placed.NameString;
                        _moveDirection = placed.Direction;
                        _moveIsItem = false;
                        _viewModel.ShowToast($"Moving {placed.NameString} — click destination");
                        _toastTimer = 2f;
                        return;
                    }
                }
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                PlacedItemObject item = hit.collider.GetComponentInParent<PlacedItemObject>();
                if (item != null)
                {
                    _moveSource = item.transform.position;
                    _moveAssetName = item.NameString;
                    _moveDirection = Direction.North;
                    _moveIsItem = true;
                    _viewModel.ShowToast($"Moving item {item.NameString} — click destination");
                    _toastTimer = 2f;
                }
            }
        }

        private void TryCompleteMove()
        {
            if (_moveSource == null)
                return;

            Vector3 destination = TileHelper.GetPointedPosition(!_moveIsItem);
            RpcMoveObject(_moveAssetName, _moveSource.Value, destination, _moveDirection, _moveIsItem, LocalConnection);
            _moveSource = null;
            _moveAssetName = null;
        }

        private void UpdateMouseOverUI()
        {
            if (_view == null)
                return;

            bool over = _view.IsPointerOverInteractiveUI(Mouse.current?.position.ReadValue() ?? Input.mousePosition);
            if (over != _mouseOverUI)
                SetMouseOverUI(over);
        }

        private void SetMouseOverUI(bool over)
        {
            _mouseOverUI = over;
            _view?.SetMouseOverUI(over);
            _inputSystem.ToggleBinding("<Mouse>/scroll/y", !over);

            if (!_hologramManager.IsDragging)
                _inputSystem.ToggleAction(_controls.Place, !over);
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

            _view.ExitRequested -= RequestExit;
            _view.ToolSelected -= OnToolSelected;
            _view.UndoRequested -= OnUndoRequested;
            _view.RedoRequested -= OnRedoRequested;
            _view.QuicksaveRequested -= HandleQuicksave;
            _view.SaveAsRequested -= OnSaveAsRequested;
            _view.LoadMapRequested -= OnLoadMapRequested;
            _view.DeleteMapRequested -= OnDeleteMapRequested;
            _view.ResetViewRequested -= OnResetViewRequested;
            _view.HideUIRequested -= OnHideUiRequested;
            _view.ShowUIRequested -= OnShowUiRequested;
            _view.GridSnapChanged -= OnGridSnapChanged;
            _view.DebugOverlayChanged -= OnDebugOverlayChanged;
            _view.LayerVisibilityChanged -= OnLayerVisibilityChanged;
            _view.ModeSelected -= OnModeSelected;
            _view.SubcategorySelected -= OnSubcategorySelected;
            _view.SearchChanged -= OnSearchChanged;
            _view.AssetSelected -= HandleAssetSelected;
        }

        private void OnToolSelected(MapEditorTool tool) => _viewModel.SetTool(tool);

        private void OnUndoRequested() => RpcUndo(LocalConnection);

        private void OnRedoRequested() => RpcRedo(LocalConnection);

        private void OnSaveAsRequested(string name) => RpcSaveMap(name, false, LocalConnection);

        private void OnLoadMapRequested(string name) => RpcLoadMap(name, LocalConnection);

        private void OnDeleteMapRequested(string name) => RpcDeleteMap(name, LocalConnection);

        private void OnResetViewRequested() => _session.ResetPosition();

        private void OnHideUiRequested()
        {
            _viewModel.HideUI = true;
            _viewModel.NotifyChanged();
        }

        private void OnShowUiRequested()
        {
            _viewModel.HideUI = false;
            _viewModel.NotifyChanged();
        }

        private void OnGridSnapChanged(bool value) => _viewModel.GridSnap = value;

        private void OnDebugOverlayChanged(bool value)
        {
            _viewModel.DebugOverlay = value;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AreaDevSettings.ShowAreaGizmos = value;
#endif
        }

        private void OnLayerVisibilityChanged(bool upper, bool lower, bool piping)
        {
            _viewModel.ShowUpperLayers = upper;
            _viewModel.ShowLowerLayers = lower;
            _viewModel.ShowPipingLayers = piping;
            MapEditorLayerVisibility.Apply(upper, lower, piping);
        }

        private void OnModeSelected(MapEditorMode mode) => _viewModel.SetMode(mode);

        private void OnSubcategorySelected(MapEditorSubcategory sub) => _viewModel.SetSubcategory(sub);

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
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcLoadMap(string mapName, NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            _persistence.Load(mapName);
            _commandService.ClearHistory();
            TargetSyncUndoState(conn, 0, 0);
            TargetToast(conn, $"Loaded {mapName}.");
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcDeleteMap(string mapName, NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            _persistence.Delete(mapName);
            TargetToast(conn, $"Deleted {mapName}.");
        }

        public void SubmitCommands(MapEditorCommandDto[] commands) =>
            RpcExecuteCommands(commands, LocalConnection);

        [ServerRpc(RequireOwnership = false)]
        private void RpcExecuteCommands(MapEditorCommandDto[] commands, NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;

            List<IMapEditorCommand> list = new();
            foreach (MapEditorCommandDto dto in commands)
                list.Add(FromDto(dto));

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
                    new ClearItemCommand(assetName, from),
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

        private static IMapEditorCommand FromDto(MapEditorCommandDto dto) =>
            dto.Kind switch
            {
                MapEditorCommandKind.PlaceTile =>
                    new PlaceTileCommand(dto.AssetName, dto.Position, dto.Direction, dto.ReplaceExisting),
                MapEditorCommandKind.PlaceItem =>
                    new PlaceItemCommand(dto.AssetName, dto.Position, dto.Direction),
                MapEditorCommandKind.ClearTile =>
                    new ClearTileCommand(dto.AssetName, dto.Position, dto.Direction),
                MapEditorCommandKind.ClearItem =>
                    new ClearItemCommand(dto.AssetName, dto.Position),
                MapEditorCommandKind.MoveTile =>
                    new MoveTileCommand(dto.AssetName, dto.PreviousPosition, dto.Position, dto.Direction),
                _ => new PlaceTileCommand(dto.AssetName, dto.Position, dto.Direction, dto.ReplaceExisting),
            };
    }
}
