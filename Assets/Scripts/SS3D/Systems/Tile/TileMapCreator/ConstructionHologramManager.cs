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
using SS3D.Systems.Tile.FloorVisuals;
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
        private bool _lastSquareDrag;
        /// <summary>
        /// Is the player currently dragging ?
        /// </summary>
        public bool IsDragging => _isDragging;
        private bool _isDragging;
        private bool _placePressActive;
        /// <summary>True when LMB went down over UI — never commit that gesture.</summary>
        private bool _pressStartedOverUi;
        private GenericObjectSo _selectedObject;
        private FloorDecalDefinition _selectedFloorDecal;
        /// <summary>
        /// List of build ghosts currently displaying in game.
        /// </summary>
        private List<ConstructionHologram> _holograms = new();
        /// <summary>Inactive holograms retained across drag resize to avoid Instantiate/Destroy thrash.</summary>
        private readonly List<ConstructionHologram> _hologramPool = new();
        private readonly List<Vector3> _dragTileBuffer = new();
        private readonly List<Vector2> _lineTileBuffer = new();
        private readonly List<MapEditorDeleteTarget> _deleteTargets = new();
        private float _invalidHoverToastCooldown;
        private string _lastHoverFailMessage;
        private string _deleteGhostAssetName;
        [SerializeField]
        private MapEditorSubSystem _mapEditor;

        public void ClearSelection()
        {
            CancelPlacementGesture(resetHolograms: false);
            _selectedObject = null;
            _selectedFloorDecal = null;
            _deleteGhostAssetName = null;
            DestroyHolograms();
        }

        /// <summary>
        /// Delete / eraser mode: keep a cursor ghost scoped to the library subcategory.
        /// </summary>
        public void EnterDeleteMode()
        {
            CancelPlacementGesture(resetHolograms: false);
            _selectedObject = null;
            _selectedFloorDecal = null;
            _isPlacingItem = false;
            _deleteGhostAssetName = null;
            DestroyHolograms();
            EnsureDeleteCursorHologram(GetPlacementPoint(forceTileSnap: true));
        }

        /// <summary>Re-resolve delete targets after subcategory / rotation changes.</summary>
        public void RefreshDeletePreview()
        {
            if (_mapEditor == null || !_mapEditor.IsDeleting)
                return;

            RefreshDeleteCursorHologram(GetPlacementPoint(forceTileSnap: true));
        }

        public void SetSelectedObject(GenericObjectSo genericObjectSo)
        {
            CancelPlacementGesture(resetHolograms: false);
            _selectedFloorDecal = null;
            _deleteGhostAssetName = null;
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

        public void SetSelectedFloorDecal(FloorDecalDefinition definition)
        {
            _selectedFloorDecal = definition;
            _selectedObject = null;
            _isPlacingItem = false;
            DestroyHolograms();
            CreateFloorDecalHologram(TileHelper.GetPointedPosition(true));
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
            if (_mapEditor == null || !_mapEditor.IsActive || !IsActiveTool(_mapEditor.CurrentTool))
            {
                if (_placePressActive || _isDragging)
                    CancelPlacementGesture(resetHolograms: _selectedObject != null || _selectedFloorDecal != null ||
                        (_mapEditor != null && _mapEditor.IsDeleting));
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

            bool hasPlacementSelection = _selectedObject != null || _selectedFloorDecal != null;
            bool deleteMode = _mapEditor != null && _mapEditor.IsDeleting;

            // Move hologram, that sticks to the mouse. Currently it exists only if player is not dragging.
            if (_holograms.Count == 1 && !_isDragging)
            {
                _holograms.First().TargetPosition = position;
                if (cursorTile != ToTile(_lastSnappedPosition))
                {
                    if (deleteMode)
                        RefreshDeleteCursorHologram(position);
                    else
                        RefreshHologram(_holograms.First());
                }
            }
            else if (deleteMode && _holograms.Count == 0 && !_isDragging)
            {
                EnsureDeleteCursorHologram(position);
            }

            if (_invalidHoverToastCooldown > 0f)
                _invalidHoverToastCooldown -= updateEvent.DeltaTime;

            bool squareDrag = _controls.SquareDrag.IsPressed();
            if (_isDragging && (hasPlacementSelection || deleteMode) &&
                (!_hasDragEndTile || cursorTile != _lastDragEndTile || squareDrag != _lastSquareDrag))
            {
                _lastDragEndTile = cursorTile;
                _hasDragEndTile = true;
                _lastSquareDrag = squareDrag;

                // Shift (Square Drag): filled rectangle. Default drag: Bresenham line.
                if (squareDrag)
                    FillSquareDrag(cursorTile, _dragTileBuffer);
                else
                    FillLineDrag(cursorTile, _dragTileBuffer);

                SyncDragHolograms(_dragTileBuffer);
            }

            _lastSnappedPosition = position;
        }

        /// <summary>
        /// Edit places/erases via a selected asset or the Eraser catalog entry; Delete is a
        /// dedicated always-erasing tool that reuses the same drag/hologram pipeline with no
        /// selected asset (see <see cref="IMapEditorHost.IsDeleting"/>).
        /// </summary>
        private static bool IsActiveTool(MapEditorTool tool) =>
            tool is MapEditorTool.Edit or MapEditorTool.Delete;

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

            if (_selectedFloorDecal != null)
                return CreateFloorDecalHologram(Vector3.zero, addToActive: false);

            if (_selectedObject != null)
                return CreateHologram(_selectedObject.PrefabAsset, Vector3.zero, addToActive: false);

            return CreateDeleteMarkerHologram(Vector3.zero, addToActive: false);
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
            _lastSquareDrag = false;

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
            _lastSquareDrag = false;
            if (resetHolograms)
                ResetHologramsToCursor();
        }

        private void ResetHologramsToCursor()
        {
            if (_mapEditor != null && _mapEditor.IsDeleting)
            {
                DestroyHolograms();
                _deleteGhostAssetName = null;
                EnsureDeleteCursorHologram(GetPlacementPoint(forceTileSnap: true));
                return;
            }

            if (_selectedFloorDecal != null)
            {
                DestroyHolograms();
                CreateFloorDecalHologram(GetPlacementPoint(forceTileSnap: true));
                return;
            }

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

            if (_holograms.Count > 0)
                _lastRegisteredDirection = _holograms.First().Direction;

            if (_mapEditor != null && _mapEditor.IsDeleting)
            {
                RefreshDeleteCursorHologram(GetPlacementPoint(forceTileSnap: true));
                _mapEditor.SetPlacementHint(
                    $"Delete {MapEditorCatalog.GetSubcategoryLabel(_mapEditor.CurrentSubcategory)} ({_lastRegisteredDirection}) — R to change face");
            }
        }

        public ConstructionHologram CreateFloorDecalHologram(Vector3 position, bool addToActive = true)
        {
            Material material = _selectedFloorDecal.MaterialOverride != null
                ? new Material(_selectedFloorDecal.MaterialOverride)
                : FloorVisualMesh.CreateCutoutMaterial(
                    _selectedFloorDecal.Texture != null
                        ? _selectedFloorDecal.Texture
                        : FloorVisualMesh.GetStripeCornerTexture(),
                    _selectedFloorDecal.Tint);

            GameObject tileObject = FloorVisualMesh.CreateQuadObject(
                "FloorDecalHologram",
                null,
                position,
                material);
            ConstructionHologram hologram = new(tileObject, position, _lastRegisteredDirection, FloorVisualMesh.SurfaceLift);
            if (addToActive)
                _holograms.Add(hologram);
            RefreshHologram(hologram);
            return hologram;
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
            _deleteGhostAssetName = null;

            if (_mapEditor.IsDeleting)
            {
                EnsureDeleteCursorHologram(GetPlacementPoint(forceTileSnap: true));
                return;
            }

            if (_selectedFloorDecal != null)
            {
                CreateFloorDecalHologram(TileHelper.GetPointedPosition(true));
                return;
            }

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
        /// Invalid cells are skipped; toasts the primary reason once per gesture (or skipped count when dragging).
        /// </summary>
        private void PlaceOnHolograms()
        {
            TileSubSystem tileSystem = SubSystems.Get<TileSubSystem>();
            if (tileSystem == null || (_selectedObject == null && _selectedFloorDecal == null))
                return;

            bool isReplacing = _controls.Replace.phase == InputActionPhase.Performed;
            int placed = 0;
            int skipped = 0;
            string skipReason = null;

            void TryPlaceAt(Vector3 position, Direction direction)
            {
                if (_selectedFloorDecal != null)
                {
                    if (!HasPlenumAt(tileSystem.CurrentMap, position))
                    {
                        skipped++;
                        skipReason ??= "Needs a plenum or catwalk underneath";
                        return;
                    }

                    tileSystem.RpcSetFloorDecal(_selectedFloorDecal.Id, position);
                    placed++;
                    return;
                }

                if (_selectedObject is TileObjectSo tileObjectSo && tileSystem.Construction != null)
                {
                    PreviewResult preview = tileSystem.Construction.TryPreviewTile(
                        tileObjectSo, position, direction, isReplacing);
                    if (!preview.CanBuild)
                    {
                        skipped++;
                        skipReason ??= preview.PrimaryMessage;
                        return;
                    }
                }

                tileSystem.RpcPlaceObject(_selectedObject.NameString, position, direction, isReplacing);
                placed++;
            }

            if (_holograms.Count == 0)
            {
                TryPlaceAt(GetPlacementPoint(), _lastRegisteredDirection);
            }
            else
            {
                foreach (ConstructionHologram buildGhost in _holograms)
                    TryPlaceAt(buildGhost.TargetPosition, buildGhost.Direction);
            }

            if (skipped > 0 && _mapEditor != null)
            {
                string message = placed == 0 && skipped == 1
                    ? skipReason
                    : $"Skipped {skipped} tiles: {skipReason}";
                _mapEditor.ShowLocalToast(message);
            }
        }

        /// <summary>
        /// Delete objects matching the current library subcategory at hologram tiles.
        /// </summary>
        private void DeleteOnHolograms()
        {
            TileSubSystem tileSystem = SubSystems.Get<TileSubSystem>();
            if (tileSystem == null || _mapEditor == null)
                return;

            MapEditorSubcategory subcategory = _mapEditor.CurrentSubcategory;

            if (MapEditorDeleteTargeting.RequiresSubcategorySelection(subcategory))
            {
                _mapEditor.ShowLocalToast("Select a subcategory to delete");
                return;
            }

            if (MapEditorDeleteTargeting.IsItemSubcategory(subcategory) ||
                _mapEditor.CurrentMode == MapEditorMode.Items)
            {
                FindAndDeleteItem();
                return;
            }

            if (_holograms.Count == 0)
            {
                ClearScopedAt(tileSystem, GetPlacementPoint(forceTileSnap: true), subcategory);
                return;
            }

            int clearedTiles = 0;
            foreach (ConstructionHologram hologram in _holograms)
            {
                if (ClearScopedAt(tileSystem, hologram.TargetPosition, subcategory))
                    clearedTiles++;
            }

            if (clearedTiles == 0)
                _mapEditor.ShowLocalToast(MapEditorDeleteTargeting.EmptyHint(subcategory, _lastRegisteredDirection));
        }

        private bool ClearScopedAt(TileSubSystem tileSystem, Vector3 position, MapEditorSubcategory subcategory)
        {
            TileMap map = tileSystem.CurrentMap;
            if (map == null)
                return false;

            if (subcategory == MapEditorSubcategory.Overlays)
            {
                if (!map.TryGetFloorDecalId(position, out ushort decalId) || decalId == 0)
                    return false;

                tileSystem.RpcClearFloorDecal(position);
                return true;
            }

            if (!map.TryGetTileLocations(position, out ITileLocation[] locations))
                return false;

            MapEditorDeleteTargeting.Resolve(subcategory, _lastRegisteredDirection, locations, _deleteTargets);
            if (_deleteTargets.Count == 0)
                return false;

            foreach (MapEditorDeleteTarget target in _deleteTargets)
            {
                if (target.IsFloorDecal)
                    tileSystem.RpcClearFloorDecal(position);
                else
                    tileSystem.RpcClearTileObject(target.AssetName, position, target.Direction);
            }

            return true;
        }

        private void EraseAtPointer()
        {
            // Kept for older call sites; Delete uses ClearScopedAt via DeleteOnHolograms.
            TileSubSystem tileSystem = SubSystems.Get<TileSubSystem>();
            if (tileSystem == null || _mapEditor == null)
                return;

            ClearScopedAt(tileSystem, GetPlacementPoint(forceTileSnap: true), _mapEditor.CurrentSubcategory);
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

            if (_selectedFloorDecal != null)
            {
                TileSubSystem tileSystem = SubSystems.Get<TileSubSystem>();
                bool canPlace = HasPlenumAt(tileSystem?.CurrentMap, hologram.TargetPosition);
                hologram.ChangeHologramColor(canPlace ? ConstructionMode.Valid : ConstructionMode.Invalid);
                ReportInvalidHover(canPlace ? null : "Needs a plenum or catwalk underneath");
                return;
            }

            if (_isPlacingItem || _selectedObject is not TileObjectSo tileObjectSo)
            {
                hologram.ChangeHologramColor(ConstructionMode.Valid);
                return;
            }

            TileSubSystem constructionTiles = SubSystems.Get<TileSubSystem>();
            if (constructionTiles?.Construction == null)
            {
                hologram.ChangeHologramColor(ConstructionMode.Valid);
                return;
            }

            bool isReplacing = _controls.Replace.phase == InputActionPhase.Performed;
            PreviewResult preview = constructionTiles.Construction
                .TryPreviewTile(tileObjectSo, hologram.TargetPosition, hologram.Direction, isReplacing);
            hologram.ChangeHologramColor(preview.CanBuild ? ConstructionMode.Valid : ConstructionMode.Invalid);
            ReportInvalidHover(preview.CanBuild ? null : preview.PrimaryMessage);
        }

        private void ReportInvalidHover(string message)
        {
            if (_mapEditor == null)
                return;

            if (string.IsNullOrEmpty(message))
            {
                if (_lastHoverFailMessage != null)
                {
                    _lastHoverFailMessage = null;
                    _mapEditor.SetPlacementHint(
                        "Edit tool active — drag a line, Shift+drag a rectangle to place");
                }

                return;
            }

            _mapEditor.SetPlacementHint(message);

            if (message == _lastHoverFailMessage && _invalidHoverToastCooldown > 0f)
                return;

            _lastHoverFailMessage = message;
            _invalidHoverToastCooldown = 0.5f;
            _mapEditor.ShowLocalToast(message);
        }

        private static bool HasPlenumAt(TileMap map, Vector3 worldPosition)
        {
            if (map == null)
                return false;

            return map.TryGetTileLocation(TileLayer.Plenum, worldPosition, out ITileLocation plenum)
                   && !plenum.IsFullyEmpty();
        }

        private void EnsureDeleteCursorHologram(Vector3 position)
        {
            if (_holograms.Count == 0)
                CreateDeleteMarkerHologram(position);

            RefreshDeleteCursorHologram(position);
        }

        private void RefreshDeleteCursorHologram(Vector3 position)
        {
            if (_holograms.Count == 0)
            {
                CreateDeleteMarkerHologram(position);
            }

            ConstructionHologram hologram = _holograms[0];
            hologram.TargetPosition = position;
            if (_holograms.Count == 1)
                _lastRegisteredDirection = hologram.Direction;

            TileSubSystem tileSystem = SubSystems.Get<TileSubSystem>();
            TileMap map = tileSystem?.CurrentMap;
            MapEditorSubcategory subcategory = _mapEditor.CurrentSubcategory;
            _deleteTargets.Clear();

            string hintAsset = null;
            if (map != null && subcategory == MapEditorSubcategory.Overlays)
            {
                if (map.TryGetFloorDecalId(position, out ushort id) && id != 0)
                {
                    _deleteTargets.Add(MapEditorDeleteTarget.FloorDecal);
                    hintAsset = "floor-decal";
                }
            }
            else if (map != null && map.TryGetTileLocations(position, out ITileLocation[] locations))
            {
                MapEditorDeleteTargeting.Resolve(subcategory, _lastRegisteredDirection, locations, _deleteTargets);
                if (_deleteTargets.Count > 0 && !_deleteTargets[0].IsFloorDecal)
                    hintAsset = _deleteTargets[0].AssetName;
            }

            if (_deleteTargets.Count > 0 && hintAsset != null && hintAsset != "floor-decal")
            {
                MaybeSwapDeleteGhostPrefab(hintAsset, position);
            }
            else if (_deleteTargets.Count == 0 || hintAsset == "floor-decal")
            {
                if (_deleteGhostAssetName != null)
                {
                    DestroyHolograms();
                    _deleteGhostAssetName = null;
                    CreateDeleteMarkerHologram(position);
                }
            }

            if (_holograms.Count > 0)
            {
                ConstructionMode mode = _deleteTargets.Count > 0 ? ConstructionMode.Delete : ConstructionMode.Invalid;
                _holograms[0].ChangeHologramColor(mode);
            }

            if (_deleteTargets.Count == 0)
            {
                string empty = MapEditorDeleteTargeting.RequiresSubcategorySelection(subcategory)
                    ? "Select a subcategory to delete"
                    : MapEditorDeleteTargeting.EmptyHint(subcategory, _lastRegisteredDirection);
                _mapEditor.SetPlacementHint(empty);
            }
            else
            {
                string label = MapEditorCatalog.GetSubcategoryLabel(subcategory);
                _mapEditor.SetPlacementHint(
                    subcategory == MapEditorSubcategory.WallAttachments
                        ? $"Delete {label} ({_lastRegisteredDirection}) — R to change face"
                        : $"Delete {label}");
            }
        }

        private void MaybeSwapDeleteGhostPrefab(string assetName, Vector3 position)
        {
            if (_deleteGhostAssetName == assetName && _holograms.Count > 0)
                return;

            TileSubSystem tileSystem = SubSystems.Get<TileSubSystem>();
            GenericObjectSo asset = tileSystem?.GetAsset(assetName);
            if (asset?.PrefabAsset == null)
                return;

            Direction dir = _holograms.Count > 0 ? _holograms[0].Direction : _lastRegisteredDirection;
            _lastRegisteredDirection = dir;
            DestroyHolograms();
            CreateHologram(asset.PrefabAsset, position);
            if (_holograms.Count > 0)
                _holograms[0].ChangeHologramColor(ConstructionMode.Delete);

            _deleteGhostAssetName = assetName;
        }

        private ConstructionHologram CreateDeleteMarkerHologram(Vector3 position, bool addToActive = true)
        {
            Material material = FloorVisualMesh.CreateCutoutMaterial(
                FloorVisualMesh.GetStripeCornerTexture(),
                new Color(1f, 0.35f, 0.35f, 0.65f));
            GameObject marker = FloorVisualMesh.CreateQuadObject(
                "DeleteHologramMarker",
                null,
                position,
                material);
            ConstructionHologram hologram = new(marker, position, _lastRegisteredDirection, FloorVisualMesh.SurfaceLift);
            hologram.ChangeHologramColor(ConstructionMode.Delete);
            if (addToActive)
                _holograms.Add(hologram);
            return hologram;
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