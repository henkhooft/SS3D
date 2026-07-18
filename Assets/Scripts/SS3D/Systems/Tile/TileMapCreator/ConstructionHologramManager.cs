using Coimbra.Services.Events;
using Coimbra.Services.PlayerLoopEvents;
using FishNet.Connection;
using FishNet.Object;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Data;
using SS3D.Data.AssetDatabases;
using SS3D.Logging;
using SS3D.Systems.Inputs;
using SS3D.Systems.Tile.MapEditor;
using SS3D.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using InputSubSystem = SS3D.Systems.Inputs.InputSubSystem;

namespace SS3D.Systems.Tile.TileMapCreator
{
    /// <summary>
    /// Class for managing tile objects, that will be used for building or deleting by the tile map menu.
    /// It handles creating construction holograms, replacing, adding or deleting tile objects and items upon placement,
    /// and placing the holograms in cool shapes like lines and squares.
    /// </summary>
    public class ConstructionHologramManager : NetworkActor
    {
        /// <summary>
        /// The last direction registered by a build ghost.
        /// </summary>
        private Direction _lastRegisteredDirection;
        private InputSubSystem _inputSystem;
        private Controls.TileCreatorActions _controls;
        private bool _isPlacingItem = false;
        /// <summary>
        /// Snapped position are positions in the center of tiles, to display tile objects ghosts properly.
        /// </summary>
        private Vector3 _lastSnappedPosition;
        /// <summary>
        /// The snapped position of the mouse, in the middle of a tile, when the player starts dragging with the mouse.
        /// </summary>
        private Vector3 _dragStartPostion;
        private Vector2Int _dragStartTile;
        private Vector2Int _lastDragEndTile;
        private bool _hasDragEndTile;
        /// <summary>
        /// Is the player currently dragging ?
        /// </summary>
        public bool IsDragging => _isDragging;
        private bool _isDragging;
        private bool _placePressActive;
        /// <summary>True when LMB went down over UI — never commit that gesture.</summary>
        private bool _pressStartedOverUi;
        private GenericObjectSo _selectedObject;
        /// <summary>
        /// List of build ghosts currently displaying in game.
        /// </summary>
        private List<ConstructionHologram> _holograms = new();
        /// <summary>Inactive holograms retained across drag resize to avoid Instantiate/Destroy thrash.</summary>
        private readonly List<ConstructionHologram> _hologramPool = new();
        private readonly List<Vector3> _dragTileBuffer = new();
        private readonly List<Vector2> _lineTileBuffer = new();
        [SerializeField]
        private MapEditorSubSystem _mapEditor;

        public void ClearSelection()
        {
            CancelPlacementGesture(resetHolograms: false);
            _selectedObject = null;
            DestroyHolograms();
        }

        public void SetSelectedObject(GenericObjectSo genericObjectSo)
        {
            CancelPlacementGesture(resetHolograms: false);
            _isPlacingItem = genericObjectSo switch
            {
                TileObjectSo => false,
                ItemObjectSo => true,
                _ => _isPlacingItem,
            };
            _selectedObject = genericObjectSo;

            DestroyHolograms();
            CreateHologram(genericObjectSo.PrefabAsset, GetPlacementPoint());
        }

        /// <summary>
        /// World point under the cursor for placement. Tiles snap when grid-snap is on (default).
        /// </summary>
        private Vector3 GetPlacementPoint(bool forceTileSnap = false)
        {
            bool snapToTile = !_isPlacingItem && (forceTileSnap || _mapEditor == null || _mapEditor.GridSnapEnabled);
            Camera camera = _mapEditor != null ? _mapEditor.PickCamera : Camera.main;
            return TileHelper.GetPointedPosition(snapToTile, camera);
        }

        protected override void OnAwake()
        {
            base.OnAwake();

#if !UNITY_SERVER
            _inputSystem = SubSystems.Get<InputSubSystem>();
            _controls = _inputSystem.Inputs.TileCreator;

            AddHandle(UpdateEvent.AddListener(HandleUpdate));
#endif
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();

#if !UNITY_SERVER
            _controls.Replace.performed += HandleReplace;
            _controls.Replace.canceled += HandleReplace;
            _controls.Rotate.performed += HandleRotate;
#endif
        }

        protected override void OnDisabled()
        {
            base.OnDisabled();

#if !UNITY_SERVER
            _controls.Replace.performed -= HandleReplace;
            _controls.Replace.canceled -= HandleReplace;
            _controls.Rotate.performed -= HandleRotate;
#endif
            CancelPlacementGesture(resetHolograms: false);
        }

        private void HandleUpdate(ref EventContext context, in UpdateEvent updateEvent)
        {
            if (_mapEditor == null || !_mapEditor.IsActive || _mapEditor.CurrentTool != MapEditorTool.Edit)
            {
                if (_placePressActive || _isDragging)
                    CancelPlacementGesture(resetHolograms: _selectedObject != null);
                return;
            }

            if (_holograms.Count == 1)
            {
                _holograms[0].UpdateRotationAndPosition();
            }

            ActivateGhosts();

            // Always resolve LMB up/down first — orbit used to early-return before this and
            // leave _placePressActive stuck so placement never worked again.
            HandlePlacementInput();

            // Freeze world picks while orbiting only. Do NOT freeze on MouseOverUI — the bottom
            // library used to be a full-width Position region, so vertical aiming (and anything
            // camera-framed over that band) looked like a "stuck" drag. Start/commit still gate on UI.
            if (_mapEditor.IsOrbiting)
                return;

            Vector3 position = GetPlacementPoint();
            // Grid tile for drag — ignore float Y / boundary jitter. Vertical mouse motion is
            // foreshortened under the orbit camera, so Vector3 equality used to rebuild the
            // Bresenham path nearly every frame while left/right stayed stable.
            Vector2Int cursorTile = ToTile(position);

            // Move hologram, that sticks to the mouse. Currently it exists only if player is not dragging.
            if (_holograms.Count == 1 && !_isDragging)
            {
                _holograms.First().TargetPosition = position;
                if (cursorTile != ToTile(_lastSnappedPosition))
                {
                    RefreshHologram(_holograms.First());
                }
            }

            if (_isDragging && _selectedObject != null &&
                (!_hasDragEndTile || cursorTile != _lastDragEndTile))
            {
                _lastDragEndTile = cursorTile;
                _hasDragEndTile = true;

                if (_controls.SquareDrag.phase == InputActionPhase.Performed)
                    FillSquareDrag(cursorTile, _dragTileBuffer);
                else
                    FillLineDrag(cursorTile, _dragTileBuffer);

                SyncDragHolograms(_dragTileBuffer);
            }

            _lastSnappedPosition = position;
        }

        private static Vector2Int ToTile(Vector3 world) =>
            new(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));

        /// <summary>
        /// Grow/shrink the active hologram list to match drag tiles without per-step Instantiate/Destroy.
        /// Validity colors are skipped during drag (camera foreshortening reshuffles many tiles);
        /// colors refresh for the single cursor hologram and after place.
        /// </summary>
        private void SyncDragHolograms(List<Vector3> tiles)
        {
            ConstructionMode dragMode = _mapEditor != null && _mapEditor.IsDeleting
                ? ConstructionMode.Delete
                : ConstructionMode.Valid;

            while (_holograms.Count < tiles.Count)
            {
                ConstructionHologram rented = RentHologram();
                rented.ChangeHologramColor(dragMode);
                _holograms.Add(rented);
            }

            while (_holograms.Count > tiles.Count)
            {
                ConstructionHologram extra = _holograms[_holograms.Count - 1];
                _holograms.RemoveAt(_holograms.Count - 1);
                ReturnHologram(extra);
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                ConstructionHologram hologram = _holograms[i];
                Vector3 tile = tiles[i];
                if (hologram.TargetPosition == tile && hologram.ActiveSelf)
                    continue;

                hologram.TargetPosition = tile;
                hologram.Hologram.transform.position = tile + new Vector3(0f, hologram.PlacementYOffset + 0.1f, 0f);
            }
        }

        private ConstructionHologram RentHologram()
        {
            if (_hologramPool.Count > 0)
            {
                ConstructionHologram pooled = _hologramPool[_hologramPool.Count - 1];
                _hologramPool.RemoveAt(_hologramPool.Count - 1);
                pooled.SetActive = true;
                return pooled;
            }

            return CreateHologram(_selectedObject.PrefabAsset, Vector3.zero, addToActive: false);
        }

        private void ReturnHologram(ConstructionHologram hologram)
        {
            if (hologram?.Hologram == null)
                return;

            hologram.SetActive = false;
            _hologramPool.Add(hologram);
        }

        private void ClearHologramPool()
        {
            for (int i = _hologramPool.Count - 1; i >= 0; i--)
                _hologramPool[i].Destroy();

            _hologramPool.Clear();
        }

        private void HandlePlacementInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            bool overUi = _mapEditor.MouseOverUI;
            bool pressed = mouse.leftButton.isPressed;
            bool justPressed = mouse.leftButton.wasPressedThisFrame;
            bool justReleased = mouse.leftButton.wasReleasedThisFrame;

            // Missed-release recovery (orbit / focus loss / UI flicker skipped wasReleasedThisFrame).
            if (_placePressActive && !pressed && !justReleased)
            {
                EndPlacementGesture(commit: false);
                return;
            }

            if (justPressed)
            {
                if (overUi)
                {
                    _pressStartedOverUi = true;
                    return;
                }

                _pressStartedOverUi = false;
                _placePressActive = true;

                if (!_isPlacingItem)
                {
                    _isDragging = true;
                    _dragStartPostion = GetPlacementPoint(forceTileSnap: true);
                    _dragStartTile = ToTile(_dragStartPostion);
                    _lastDragEndTile = _dragStartTile;
                    _hasDragEndTile = true;
                }

                return;
            }

            if (!_placePressActive)
                return;

            if (!justReleased)
                return;

            // Commit only if the gesture began and ended off UI. Ending over chrome cancels
            // without placing (and rebuilds a single cursor hologram).
            bool commit = !_pressStartedOverUi && !overUi;
            EndPlacementGesture(commit);
        }

        private void EndPlacementGesture(bool commit)
        {
            _placePressActive = false;
            _isDragging = false;
            _pressStartedOverUi = false;
            _hasDragEndTile = false;

            if (commit)
            {
                PerformPlaceOrDelete();
                return;
            }

            ResetHologramsToCursor();
        }

        private void CancelPlacementGesture(bool resetHolograms)
        {
            _placePressActive = false;
            _isDragging = false;
            _pressStartedOverUi = false;
            _hasDragEndTile = false;
            if (resetHolograms)
                ResetHologramsToCursor();
        }

        private void ResetHologramsToCursor()
        {
            if (_selectedObject == null)
            {
                DestroyHolograms();
                return;
            }

            DestroyHolograms();
            CreateHologram(_selectedObject.PrefabAsset, GetPlacementPoint());
        }

        /// <summary>
        /// Rotate all existing ghosts in the next allowed rotation.
        /// </summary>
        public void SetNextRotation()
        {
            foreach (ConstructionHologram hologram in _holograms)
            {
                hologram.SetNextRotation();
                RefreshHologram(hologram);
            }
            _lastRegisteredDirection = _holograms.First().Direction;
        }

        /// <summary>
        /// Instantiate in the correct position and rotation a single hologram.
        /// </summary>
        /// <param name="addToActive">False when renting into the drag pool path (caller adds).</param>
        public ConstructionHologram CreateHologram(ObjectAssetReference prefabAsset, Vector3 position, bool addToActive = true)
        {
            GameObject prefab = Assets.Get<GameObject>(prefabAsset);
            GameObject tileObject = Instantiate(prefab);
            float placementYOffset = _selectedObject is TileObjectSo tileObjectSo
                ? tileObjectSo.placementYOffset
                : 0f;

            ConstructionHologram hologram = new(tileObject, position, _lastRegisteredDirection, placementYOffset);
            tileObject.transform.rotation = Quaternion.Euler(0, TileHelper.GetRotationAngle(hologram.Direction), 0);
            tileObject.transform.position = hologram.TargetPosition + new Vector3(0, placementYOffset + 0.1f, 0);
            if (addToActive)
                _holograms.Add(hologram);
            RefreshHologram(hologram);
            return hologram;
        }

        /// <summary>
        /// Destroy all existing holograms.
        /// </summary>
        public void DestroyHolograms()
        {
            for (int i = _holograms.Count - 1; i >= 0; i--)
            {
                _holograms[i].Destroy();
            }
            _holograms.Clear();
            ClearHologramPool();
        }

        /// <summary>
        /// Called upon control triggered to rotate holograms.
        /// </summary>
        private void HandleRotate(InputAction.CallbackContext context)
        {
            SetNextRotation();
        }

        private void PerformPlaceOrDelete()
        {
            if (!_mapEditor.IsDeleting)
            {
                PlaceOnHolograms();
            }
            else
            {
                DeleteOnHolograms();
            }

            DestroyHolograms();

            if (_selectedObject == null)
                return;

            CreateHologram(_selectedObject.PrefabAsset, GetPlacementPoint());
        }

        /// <summary>
        /// Called when the control is triggered to replace already present tile objects with new ones.
        /// </summary>
        /// <param name="context"></param>
        private void HandleReplace(InputAction.CallbackContext context)
        {
            foreach (ConstructionHologram buildGhost in _holograms)
            {
                RefreshHologram(buildGhost);
            }
        }

        /// <summary>
        /// Place all objects on the tilemap that are at the same locations as existing holograms.
        /// </summary>
        private void PlaceOnHolograms()
        {
            TileSubSystem tileSystem = SubSystems.Get<TileSubSystem>();
            if (tileSystem == null || _selectedObject == null)
                return;

            bool isReplacing = _controls.Replace.phase == InputActionPhase.Performed;

            if (_holograms.Count == 0)
            {
                Vector3 position = GetPlacementPoint();
                tileSystem.RpcPlaceObject(_selectedObject.NameString, position, _lastRegisteredDirection, isReplacing);
                return;
            }

            foreach (ConstructionHologram buildGhost in _holograms)
            {
                tileSystem.RpcPlaceObject(_selectedObject.NameString, buildGhost.TargetPosition, buildGhost.Direction, isReplacing);
            }
        }

        /// <summary>
        /// Delete all objects, that are at the same locations as existing holograms.
        /// </summary>
        private void DeleteOnHolograms()
        {
            TileSubSystem tileSystem = SubSystems.Get<TileSubSystem>();
            if (tileSystem == null)
                return;

            if (_isPlacingItem)
            {
                FindAndDeleteItem();
                return;
            }

            if (_holograms.Count > 0 && _selectedObject != null)
            {
                foreach (ConstructionHologram hologram in _holograms)
                {
                    tileSystem.RpcClearTileObject(_selectedObject.NameString, hologram.TargetPosition, hologram.Direction);
                }

                return;
            }

            EraseAtPointer();
        }

        private void EraseAtPointer()
        {
            TileSubSystem tileSystem = SubSystems.Get<TileSubSystem>();
            TileMap map = tileSystem?.CurrentMap;
            if (map == null)
                return;

            Vector3 position = GetPlacementPoint(forceTileSnap: true);
            if (!map.TryGetTileLocations(position, out ITileLocation[] locations))
                return;

            bool clearedAny = false;
            foreach (ITileLocation location in locations)
            {
                foreach (PlacedTileObject placed in location.GetAllPlacedObject())
                {
                    tileSystem.RpcClearTileObject(placed.NameString, position, placed.Direction);
                    clearedAny = true;
                }
            }

            if (!clearedAny)
                FindAndDeleteItem();
        }

        /// <summary>
        /// Update material of holograms based build (or anything else) mode and holograms position.
        /// Uses local <see cref="IConstructionService.TryPreviewTile"/> — never ServerRpc per ghost;
        /// drag used to spam RpcSendCanBuild and hitch so hard the path looked frozen.
        /// </summary>
        private void RefreshHologram(ConstructionHologram hologram)
        {
            if (_mapEditor != null && _mapEditor.IsDeleting)
            {
                hologram.ChangeHologramColor(ConstructionMode.Delete);
                return;
            }

            if (_isPlacingItem || _selectedObject is not TileObjectSo tileObjectSo)
            {
                hologram.ChangeHologramColor(ConstructionMode.Valid);
                return;
            }

            TileSubSystem tileSystem = SubSystems.Get<TileSubSystem>();
            if (tileSystem?.Construction == null)
            {
                hologram.ChangeHologramColor(ConstructionMode.Valid);
                return;
            }

            bool isReplacing = _controls.Replace.phase == InputActionPhase.Performed;
            bool canBuild = tileSystem.Construction
                .TryPreviewTile(tileObjectSo, hologram.TargetPosition, hologram.Direction, isReplacing).CanBuild;
            hologram.ChangeHologramColor(canBuild ? ConstructionMode.Valid : ConstructionMode.Invalid);
        }

        /// <summary>
        /// Activate all buildGhosts. This method is important, because for some reason network objects disable themselves after a few frames.
        /// </summary>
        private void ActivateGhosts()
        {
            foreach (ConstructionHologram buildGhost in _holograms)
            {
                if (!buildGhost.ActiveSelf)
                {
                    buildGhost.SetActive = true;
                }
            }
        }

        private void FillLineDrag(Vector2Int endTile, List<Vector3> into)
        {
            into.Clear();
            MathUtility.FillTilesOnLine(
                _dragStartTile.x, _dragStartTile.y,
                endTile.x, endTile.y,
                _lineTileBuffer);
            for (int i = 0; i < _lineTileBuffer.Count; i++)
            {
                Vector2 tile = _lineTileBuffer[i];
                into.Add(new Vector3(tile.x, 0f, tile.y));
            }
        }

        private void FillSquareDrag(Vector2Int endTile, List<Vector3> into)
        {
            into.Clear();
            int x1 = Math.Min(_dragStartTile.x, endTile.x);
            int x2 = Math.Max(_dragStartTile.x, endTile.x);
            int y1 = Math.Min(_dragStartTile.y, endTile.y);
            int y2 = Math.Max(_dragStartTile.y, endTile.y);

            for (int i = y1; i <= y2; i++)
            {
                for (int j = x1; j <= x2; j++)
                {
                    into.Add(new Vector3(j, 0f, i));
                }
            }
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void RpcSendCanBuild(string tileObjectSoName, Vector3 placePosition, Direction dir, bool replaceExisting, NetworkConnection conn)
        {
            // Kept for FishNet codegen / older callers; preview is local via RefreshHologram.
            if (!MapEditorPermissions.TryAuthorize(conn))
            {
                RpcReceiveCanBuild(conn, placePosition, false);
                return;
            }

            TileSubSystem tileSystem = SubSystems.Get<TileSubSystem>();

            TileObjectSo tileObjectSo = (TileObjectSo)tileSystem.GetAsset(tileObjectSoName);

            if (tileObjectSo == null)
            {
                Log.Error(this, "Asset is not found");
                return;
            }

            bool canBuild = tileSystem.Construction.TryPreviewTile(tileObjectSo, placePosition, dir, replaceExisting).CanBuild;
            RpcReceiveCanBuild(conn, placePosition, canBuild);
        }

        [TargetRpc]
        private void RpcReceiveCanBuild(NetworkConnection conn, Vector3 placePosition, bool canBuild)
        {
            // Find correct hologram to update its material
            for (int i = 0; i < _holograms.Count; i++)
            {
                ConstructionHologram hologram = _holograms[i];
                if (hologram.TargetPosition != placePosition) continue;

                if (canBuild)
                {
                    hologram.ChangeHologramColor(ConstructionMode.Valid);
                }
                else
                {
                    hologram.ChangeHologramColor(ConstructionMode.Invalid);
                }
                return;
            }
        }

        /// <summary>
        /// Method called when trying to delete an item from the tilemap (as opposed to a tile object).
        /// Items need a special method because they are not tied to specific coordinates like tile objects.
        /// </summary>
        private void FindAndDeleteItem()
        {
            Vector2 screenPosition = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : (Vector2)Input.mousePosition;
            Camera camera = _mapEditor != null ? _mapEditor.PickCamera : Camera.main;
            if (camera == null)
                return;

            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hitInfo))
            {
                PlacedItemObject placedItem = hitInfo.collider.gameObject.GetComponent<PlacedItemObject>();
                if (placedItem != null)
                {
                    SubSystems.Get<TileSubSystem>()?.RpcClearItemObject(
                        placedItem.NameString,
                        placedItem.gameObject.transform.position);
                }
            }
        }
    }
}