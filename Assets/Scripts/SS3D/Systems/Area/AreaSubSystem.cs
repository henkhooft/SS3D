using Coimbra.Services.Events;
using Coimbra.Services.PlayerLoopEvents;
using FishNet.Object;
using SS3D.Core;
using SS3D.Core.Behaviours;
using SS3D.Core.WorldReadiness;
using SS3D.Systems.Tile;
using System;
using System.Collections.Generic;
using SS3D.Systems.Electricity;
using System.Linq;
using UnityEngine;

namespace SS3D.Systems.Area
{
    /// <summary>
    /// APC-seeded area flood-fill and per-tile area-id registry.
    /// </summary>
    public sealed class AreaSubSystem : NetworkSubSystem, ITileMutationObserver, IAreaLightingStateSource, IWorldReady
    {
        public event Action WhenReady;

        public event Action<AreaId, AreaLightingState> OnAreaLightingStateChanged;

        public event Action<AreaId, bool> OnAreaLightingSwitchChanged;

        public event Action OnAreaVisualsDirty;

        public bool IsReady { get; private set; }

        public AreaFloorVisualCache FloorVisualCache { get; } = new();

        private readonly AreaRegistry _registry = new();
        private readonly List<IAreaApcOrigin> _registeredApcs = new();
        private readonly HashSet<IAreaApcOrigin> _overlapFlaggedApcs = new();
        private readonly Dictionary<AreaId, AreaLightingState> _lightingStates = new();
        private readonly Dictionary<AreaId, bool> _lightingSwitchOn = new();
        private Dictionary<Vector3, SavedAreaRecord> _pendingSavedByApcPosition;

        private TileMap _map;
        private ITileQueryService _query;
        private AreaFloodFillService _floodFill;
        private bool _electricityTickSubscribed;
        private bool _templateRestoreActive;
        private bool _mapWired;

        /// <summary>
        /// When true, <see cref="RegisterApc"/> queues APCs without flooding. Used while
        /// <see cref="TileMap.Load"/> is still placing tiles across chunks — flooding mid-load
        /// only claims tiles that already exist (often "front and right", never the unloaded side).
        /// </summary>
        private bool _deferAreaFlood;

        /// <summary>
        /// TileMap notifies <see cref="OnTileCleared"/> before the occupant is removed; defer
        /// reflood to the next update so occupancy matches the post-clear world (same pitfall as atmos).
        /// </summary>
        private bool _pendingLiveBoundaryRecompute;

        public override void OnStartServer()
        {
            base.OnStartServer();

            TileSubSystem tileSubSystem = SubSystems.Get<TileSubSystem>();
            tileSubSystem.OnMapCreated += HandleTileMapCreated;

            if (tileSubSystem.CurrentMap != null)
                CompleteSetup(tileSubSystem);

            AddHandle(UpdateEvent.AddListener(HandleUpdate));
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsServer)
                FloorVisualCache.OnDirty += HandleFloorVisualCacheDirty;
        }

        protected override void OnDestroyed()
        {
            if (SubSystems.TryGet(out TileSubSystem tileSubSystem))
                tileSubSystem.OnMapCreated -= HandleTileMapCreated;

            FloorVisualCache.OnDirty -= HandleFloorVisualCacheDirty;
            UnsubscribeElectricityTicks();
            SubSystems.Get<TileSubSystem>()?.UnregisterTileMutationObserver(this);
            base.OnDestroyed();
        }

        private void HandleFloorVisualCacheDirty() => OnAreaVisualsDirty?.Invoke();

        private void HandleTileMapCreated()
        {
            if (_mapWired)
                return;

            if (SubSystems.TryGet(out TileSubSystem tileSubSystem))
                CompleteSetup(tileSubSystem);
        }

        private void CompleteSetup(TileSubSystem tileSubSystem)
        {
            if (_mapWired || tileSubSystem.CurrentMap == null)
                return;

            _map = tileSubSystem.CurrentMap;
            _query = tileSubSystem.QueryService;
            _floodFill = new AreaFloodFillService(_map, _query);

            tileSubSystem.RegisterTileMutationObserver(this);

            _mapWired = true;
            GameplayLightGuard.DisableOrphanSceneLights();
            SubscribeElectricityTicks();
        }

        public bool TryGetEffectiveApcForDevice(IElectricDevice device, out IApcChannelSource apc)
        {
            apc = null;
            if (device?.TileObject == null)
            {
                return false;
            }

            if (!TryGetAreaForDevice(device.TileObject, out AreaRecord record))
            {
                return false;
            }

            return TryGetAreaApc(record.Id, out apc);
        }

        public bool TryGetLightingState(AreaId areaId, out AreaLightingState state)
        {
            return _lightingStates.TryGetValue(areaId, out state);
        }

        /// <summary>
        /// Re-derives Normal/Emergency/Dark from APC channels and stats immediately (server).
        /// Use after APC channel toggles instead of waiting for the next electricity tick.
        /// </summary>
        [Server]
        public void RefreshAreaLightingStates()
        {
            UpdateAreaLightingStates();
        }

        public bool TryGetLightingStateForTile(TileCoord coord, out AreaLightingState state)
        {
            state = default;
            if (!TryGetAreaForTile(coord, out AreaRecord record))
            {
                return false;
            }

            return TryGetLightingState(record.Id, out state);
        }

        public bool TryGetAreaLightingSwitchOn(AreaId areaId, out bool on)
        {
            if (_lightingSwitchOn.TryGetValue(areaId, out on))
            {
                return true;
            }

            if (IsServer && _registry.TryGet(areaId, out AreaRecord record))
            {
                on = record.LightingSwitchOn;
                return true;
            }

            on = true;
            return false;
        }

        [Server]
        public bool ToggleAreaLightingSwitch(AreaId areaId)
        {
            if (!_registry.TryGet(areaId, out AreaRecord record))
            {
                return false;
            }

            record.LightingSwitchOn = !record.LightingSwitchOn;
            ApplyLightingSwitchChange(areaId, record.LightingSwitchOn);
            UpdateAreaLightingStates(pushObservers: false);
            // One snapshot after switch + re-derived state so clients never see switch/state skew.
            PushLightingSnapshotToObservers();
            return true;
        }

        public bool TryGetAreaForTile(TileCoord coord, out AreaRecord record)
        {
            record = null;
            if (_map == null || !_map.TryGetAreaId(coord, out ushort areaId))
                return false;

            return _registry.TryGet(new AreaId(areaId), out record);
        }

        public bool TryGetAreaForDevice(PlacedTileObject tileObject, out AreaRecord record)
        {
            record = null;
            if (tileObject == null)
            {
                return false;
            }

            // Wall-mounted devices can share the same wall tile on opposite sides of a wall.
            // In that case the wall tile's stored area (if any) is ambiguous; the correct area is
            // always the tile "in front" of the device's facing direction.
            if (tileObject.Layer == TileLayer.WallMountHigh || tileObject.Layer == TileLayer.WallMountLow)
            {
                TileCoord inFrontTile = AreaDeviceTileResolver.GetTileInFront(tileObject);
                return TryGetAreaForTile(inFrontTile, out record);
            }

            TileCoord origin = AreaDeviceTileResolver.GetOriginTile(tileObject);
            if (TryGetAreaForTile(origin, out record))
            {
                return true;
            }

            TileCoord inFront = AreaDeviceTileResolver.GetTileInFront(tileObject);
            return TryGetAreaForTile(inFront, out record);
        }

        /// <summary>
        /// Resolves area id for a device on host (live registry) or pure clients (floor-cache snapshot).
        /// </summary>
        public bool TryResolveAreaIdForDevice(PlacedTileObject tileObject, out AreaId areaId)
        {
            areaId = default;
            if (TryGetAreaForDevice(tileObject, out AreaRecord record))
            {
                areaId = record.Id;
                return true;
            }

            return AreaDeviceTileResolver.TryResolveAreaIdFromFloorCache(FloorVisualCache, tileObject, out areaId);
        }

        /// <summary>
        /// Departmental tint from live registry (host) or floor-cache snapshot (clients).
        /// </summary>
        public bool TryGetDepartmentalLightTint(AreaId areaId, out Color tint)
        {
            if (_registry.TryGet(areaId, out AreaRecord record) && record.HasDepartmentalLightTint)
            {
                tint = record.DepartmentalLightTint;
                return true;
            }

            return FloorVisualCache.TryGetTint(areaId.Value, out tint);
        }

        public bool TryGetAreaApc(AreaId areaId, out IApcChannelSource apc)
        {
            apc = null;
            if (!_registry.TryGet(areaId, out AreaRecord record) || record.Apc == null)
                return false;

            if (record.Apc is IApcChannelSource channelSource)
            {
                apc = channelSource;
                return true;
            }

            return false;
        }

        public IReadOnlyList<AreaRecord> GetAllAreas() => _registry.GetAllAreas();

        /// <summary>
        /// Suppress flood-fill while the station template is still placing tiles. Call
        /// <see cref="EndDeferredAreaFlood"/> after load/restore completes.
        /// </summary>
        [Server]
        public void BeginDeferredAreaFlood()
        {
            _deferAreaFlood = true;
        }

        /// <summary>
        /// Recompute per-tile area ids from every registered APC now that the map is complete.
        /// Preserves existing <see cref="AreaRecord"/> metadata (names, tags, tints, access, switches).
        /// Always notifies world readiness (including no-op / early-out paths).
        /// </summary>
        [Server]
        public void EndDeferredAreaFlood()
        {
            if (_deferAreaFlood)
            {
                _deferAreaFlood = false;

                if (_floodFill != null && _map != null && _registeredApcs.Count > 0)
                {
                    // Template restore may have linked APCs to saved records without flooding.
                    // Mid-load RegisterApc may have queued APCs with no records yet.
                    bool anyLinked = false;
                    foreach (IAreaApcOrigin apc in _registeredApcs)
                    {
                        if (_registry.TryGetApcArea(apc, out _))
                        {
                            anyLinked = true;
                            break;
                        }
                    }

                    if (anyLinked)
                    {
                        RefloodAllAreaTilesPreservingMetadata();
                    }
                    else
                    {
                        RebuildAllAreasFromApcs();
                    }
                }
            }

            MarkAreasReady();
        }

        private void MarkAreasReady()
        {
            if (!_mapWired)
            {
                if (SubSystems.TryGet(out WorldReadiness.WorldReadinessSubSystem readinessEarly))
                {
                    readinessEarly.NotifyAreasFlooded();
                }

                return;
            }

            if (!IsReady)
            {
                IsReady = true;
                WhenReady?.Invoke();
            }

            if (SubSystems.TryGet(out WorldReadiness.WorldReadinessSubSystem readiness))
            {
                readiness.NotifyAreasFlooded();
            }
        }

        [Server]
        public void RegisterApc(IAreaApcOrigin apc)
        {
            if (apc == null || _registeredApcs.Contains(apc))
                return;

            _registeredApcs.Add(apc);

            if (_templateRestoreActive && TryLinkApcDuringTemplateRestore(apc))
            {
                UpdateOverlapWarnings();
                TryCompleteTemplateRestore();
                InvalidateElectricityConsumerIndex();
                NotifyAreaVisualsChanged();
                return;
            }

            // Map load still placing tiles — queue only; EndDeferredAreaFlood will flood once.
            if (_deferAreaFlood)
            {
                return;
            }

            if (_registry.TryGetApcArea(apc, out AreaId existingArea))
            {
                _floodFill.ClearAreaTiles(existingArea);
                _registry.Unregister(existingArea);
            }

            AreaId areaId = _registry.AllocateId();
            var record = new AreaRecord
            {
                Id = areaId,
                DisplayName = string.IsNullOrWhiteSpace(apc.DisplayName) ? "Unnamed Area" : apc.DisplayName,
                ParentTag = string.Empty,
                Apc = apc,
            };
            _registry.Register(record);
            _lightingSwitchOn[areaId] = record.LightingSwitchOn;

            var claimedTiles = BuildClaimedTilesExcluding(areaId);
            _floodFill.FloodFromApc(apc, areaId, claimedTiles);
            _floodFill.AssignDoorTileAreas();
            UpdateOverlapWarnings();
            InvalidateElectricityConsumerIndex();
            NotifyAreaVisualsChanged();
        }

        [Server]
        public void UnregisterApc(IAreaApcOrigin apc)
        {
            if (apc == null || !_registeredApcs.Remove(apc))
                return;

            if (!_registry.TryGetApcArea(apc, out AreaId areaId))
                return;

            _floodFill.ClearAreaTiles(areaId);
            _registry.Unregister(areaId);
            _lightingStates.Remove(areaId);
            _lightingSwitchOn.Remove(areaId);
            _overlapFlaggedApcs.Remove(apc);
            apc.SetMultipleApcsInArea(false);
            UpdateOverlapWarnings();
            InvalidateElectricityConsumerIndex();
            NotifyAreaVisualsChanged();
        }

        [Server]
        public void RebuildAllAreasFromApcs()
        {
            _map.ClearAllAreaIds();
            _registry.Clear();
            _overlapFlaggedApcs.Clear();
            _lightingStates.Clear();
            _lightingSwitchOn.Clear();

            List<IAreaApcOrigin> apcs = _registeredApcs
                .OrderBy(apc => apc.OriginTile.Grid.x)
                .ThenBy(apc => apc.OriginTile.Grid.y)
                .ToList();

            var claimedTiles = new HashSet<TileCoord>();

            foreach (IAreaApcOrigin apc in apcs)
            {
                AreaId areaId = _registry.AllocateId();
                var record = new AreaRecord
                {
                    Id = areaId,
                    DisplayName = string.IsNullOrWhiteSpace(apc.DisplayName) ? "Unnamed Area" : apc.DisplayName,
                    ParentTag = string.Empty,
                    Apc = apc,
                };
                _registry.Register(record);
                _lightingSwitchOn[areaId] = record.LightingSwitchOn;
                _floodFill.FloodFromApc(apc, areaId, claimedTiles);
            }

            _floodFill.AssignDoorTileAreas();
            UpdateOverlapWarnings();
            InvalidateElectricityConsumerIndex();
            NotifyAreaVisualsChanged();
        }

        [Server]
        public void RebuildAreaFromApc(IAreaApcOrigin apc)
        {
            if (apc == null)
                return;

            if (_registry.TryGetApcArea(apc, out AreaId existingArea))
            {
                _floodFill.ClearAreaTiles(existingArea);
                _registry.Unregister(existingArea);
            }

            AreaId areaId = _registry.AllocateId();
            var record = new AreaRecord
            {
                Id = areaId,
                DisplayName = string.IsNullOrWhiteSpace(apc.DisplayName) ? "Unnamed Area" : apc.DisplayName,
                ParentTag = string.Empty,
                Apc = apc,
            };
            _registry.Register(record);
            _lightingSwitchOn[areaId] = record.LightingSwitchOn;

            var claimedTiles = BuildClaimedTilesExcluding(areaId);
            _floodFill.FloodFromApc(apc, areaId, claimedTiles);
            _floodFill.AssignDoorTileAreas();
            UpdateOverlapWarnings();
            InvalidateElectricityConsumerIndex();
            NotifyAreaVisualsChanged();
        }

        /// <summary>
        /// Clears tile area ids and floods again from registered APCs, keeping existing AreaRecords.
        /// </summary>
        [Server]
        public void RefloodAllAreaTilesPreservingMetadata()
        {
            if (_floodFill == null || _map == null)
            {
                return;
            }

            _map.ClearAllAreaIds();
            _overlapFlaggedApcs.Clear();

            List<IAreaApcOrigin> apcs = _registeredApcs
                .OrderBy(apc => apc.OriginTile.Grid.x)
                .ThenBy(apc => apc.OriginTile.Grid.y)
                .ToList();

            var claimedTiles = new HashSet<TileCoord>();

            foreach (IAreaApcOrigin apc in apcs)
            {
                if (!_registry.TryGetApcArea(apc, out AreaId areaId))
                {
                    // APC registered during deferred load without a saved record — allocate one.
                    areaId = _registry.AllocateId();
                    var record = new AreaRecord
                    {
                        Id = areaId,
                        DisplayName = string.IsNullOrWhiteSpace(apc.DisplayName) ? "Unnamed Area" : apc.DisplayName,
                        ParentTag = string.Empty,
                        Apc = apc,
                    };
                    _registry.Register(record);
                    _lightingSwitchOn[areaId] = record.LightingSwitchOn;
                }

                _floodFill.FloodFromApc(apc, areaId, claimedTiles);
            }

            _floodFill.AssignDoorTileAreas();
            UpdateOverlapWarnings();
            InvalidateElectricityConsumerIndex();
        }

        [Server]
        public void RenameArea(AreaId areaId, string displayName)
        {
            if (!_registry.TryGet(areaId, out AreaRecord record))
                return;

            record.DisplayName = displayName;
        }

        [Server]
        public void SetParentTag(AreaId areaId, string tag)
        {
            if (!_registry.TryGet(areaId, out AreaRecord record))
                return;

            record.ParentTag = tag ?? string.Empty;
        }

        [Server]
        public void SetDepartmentalLightTint(AreaId areaId, Color tint)
        {
            if (!_registry.TryGet(areaId, out AreaRecord record))
                return;

            record.HasDepartmentalLightTint = true;
            record.DepartmentalLightTint = tint;
            NotifyAreaVisualsChanged();
        }

        [Server]
        public void SetDefaultRequiredAccess(AreaId areaId, IdAccess.AccessMask requiredAccess)
        {
            if (!_registry.TryGet(areaId, out AreaRecord record))
            {
                return;
            }

            record.DefaultRequiredAccess = requiredAccess;
        }

        public bool TryGetAreaForWorldPosition(Vector3 worldPosition, out AreaRecord record)
        {
            record = null;
            if (_query == null)
            {
                return false;
            }

            TileCoord coord = _query.WorldToTile(worldPosition);
            return TryGetAreaForTile(coord, out record);
        }

        public void ClearDepartmentalLightTint(AreaId areaId)
        {
            if (!_registry.TryGet(areaId, out AreaRecord record))
                return;

            record.HasDepartmentalLightTint = false;
            record.DepartmentalLightTint = default;
            if (IsServer)
                NotifyAreaVisualsChanged();
        }

        public SavedAreaRecord[] BuildSavedAreaRecords()
        {
            var saved = new List<SavedAreaRecord>();
            foreach (AreaRecord record in _registry.GetAllAreas())
            {
                if (record.Apc == null)
                    continue;

                Vector3 world = _query.TileToWorld(record.Apc.OriginTile);
                saved.Add(new SavedAreaRecord
                {
                    id = record.Id.Value,
                    displayName = record.DisplayName,
                    parentTag = record.ParentTag,
                    apcWorldPosition = world,
                    hasDepartmentalLightTint = record.HasDepartmentalLightTint,
                    departmentalLightTint = record.DepartmentalLightTint,
                    defaultRequiredAccessBits = record.DefaultRequiredAccess.Value,
                    lightingSwitchOn = record.LightingSwitchOn,
                });
            }

            return saved.ToArray();
        }

        public void OnTilePlaced(ITileOccupant occupant, TileCoord coord)
        {
            if (ShouldRecomputeForOccupant(occupant))
                QueueLiveBoundaryRecompute();
        }

        public void OnTileCleared(ITileOccupant occupant, TileCoord coord, TileLayer layer)
        {
            if (layer == TileLayer.Turf && ShouldRecomputeForOccupant(occupant))
                QueueLiveBoundaryRecompute();
        }

        public void OnChunkCreated(TileChunkRef chunk) { }

        public void OnTileStateChanged(TileCoord coord)
        {
            // Integrity Cracked changes airtightness for atmos, not Area boundaries (wall still present).
        }

        private void HandleUpdate(ref EventContext context, in UpdateEvent updateEvent)
        {
            if (!_pendingLiveBoundaryRecompute)
                return;

            _pendingLiveBoundaryRecompute = false;
            RequestLiveBoundaryRecompute();
        }

        private void QueueLiveBoundaryRecompute()
        {
            _pendingLiveBoundaryRecompute = true;
        }

        /// <summary>
        /// Phase 1: full reflood preserving AreaRecord metadata. True local-region flood is a follow-up.
        /// </summary>
        [Server]
        public void RequestLiveBoundaryRecompute()
        {
            if (_deferAreaFlood || _floodFill == null || _map == null)
                return;

            if (_registeredApcs.Count == 0)
                return;

            RefloodAllAreaTilesPreservingMetadata();
            NotifyAreaVisualsChanged();
        }

        private static bool ShouldRecomputeForOccupant(ITileOccupant occupant)
        {
            if (occupant is not PlacedTileObject placed)
                return false;

            return placed.GenericType == TileObjectGenericType.Wall
                || placed.GenericType == TileObjectGenericType.Door;
        }

        [Server]
        public void BeginTemplateRestore(IReadOnlyList<SavedAreaRecord> savedAreas)
        {
            _templateRestoreActive = true;
            _pendingSavedByApcPosition = new Dictionary<Vector3, SavedAreaRecord>();

            if (savedAreas == null)
            {
                return;
            }

            foreach (SavedAreaRecord saved in savedAreas)
            {
                _pendingSavedByApcPosition[saved.apcWorldPosition] = saved;
            }
        }

        [Server]
        public void RestoreFromSave(IReadOnlyList<SavedAreaRecord> savedAreas)
        {
            if (savedAreas == null || savedAreas.Count == 0)
            {
                return;
            }

            RestoreRegistryFromSave(savedAreas);
            LinkRegisteredApcsDuringTemplateRestore();
            UpdateAreaLightingStates();
            TryCompleteTemplateRestore();
            NotifyAreaVisualsChanged();
        }

        [Server]
        public void EndTemplateRestore()
        {
            _templateRestoreActive = false;
            _pendingSavedByApcPosition = null;
        }

        private bool TryLinkApcDuringTemplateRestore(IAreaApcOrigin apc)
        {
            if (_pendingSavedByApcPosition == null || _query == null)
            {
                return false;
            }

            Vector3 world = _query.TileToWorld(apc.OriginTile);
            if (!_pendingSavedByApcPosition.TryGetValue(world, out SavedAreaRecord saved))
            {
                return false;
            }

            var areaId = new AreaId(saved.id);
            if (!_registry.TryGet(areaId, out AreaRecord record))
            {
                return false;
            }

            record.Apc = apc;
            _registry.Register(record);
            _lightingSwitchOn[areaId] = record.LightingSwitchOn;
            return true;
        }

        private void LinkRegisteredApcsDuringTemplateRestore()
        {
            foreach (IAreaApcOrigin apc in _registeredApcs)
            {
                TryLinkApcDuringTemplateRestore(apc);
            }

            UpdateOverlapWarnings();
        }

        private void TryCompleteTemplateRestore()
        {
            if (!_templateRestoreActive)
            {
                return;
            }

            if (_pendingSavedByApcPosition == null || _pendingSavedByApcPosition.Count == 0)
            {
                EndTemplateRestore();
                return;
            }

            foreach (SavedAreaRecord saved in _pendingSavedByApcPosition.Values)
            {
                var areaId = new AreaId(saved.id);
                if (!_registry.TryGet(areaId, out AreaRecord record) || record.Apc == null)
                {
                    return;
                }
            }

            EndTemplateRestore();
        }

        private void RestoreRegistryFromSave(IReadOnlyList<SavedAreaRecord> savedAreas)
        {
            _registry.Clear();
            ushort highestId = 0;

            foreach (SavedAreaRecord saved in savedAreas)
            {
                highestId = Math.Max(highestId, saved.id);
                _registry.Register(new AreaRecord
                {
                    Id = new AreaId(saved.id),
                    DisplayName = saved.displayName,
                    ParentTag = saved.parentTag,
                    Apc = null,
                    HasDepartmentalLightTint = saved.hasDepartmentalLightTint,
                    DepartmentalLightTint = saved.departmentalLightTint,
                    DefaultRequiredAccess = new IdAccess.AccessMask(saved.defaultRequiredAccessBits),
                    LightingSwitchOn = saved.lightingSwitchOn,
                });

                _lightingSwitchOn[new AreaId(saved.id)] = saved.lightingSwitchOn;
            }

            _registry.EnsureNextIdAbove(highestId);
        }

        private HashSet<TileCoord> BuildClaimedTilesExcluding(AreaId excludeAreaId)
        {
            var claimed = new HashSet<TileCoord>();

            foreach (TileChunk chunk in _map.GetAllChunks())
            {
                for (int x = 0; x < TileChunk.ChunkSize; x++)
                {
                    for (int y = 0; y < TileChunk.ChunkSize; y++)
                    {
                        ushort areaId = chunk.GetAreaId(x, y);
                        if (areaId == AreaId.None || areaId == excludeAreaId.Value)
                            continue;

                        claimed.Add(_query.WorldToTile(chunk.GetWorldPosition(x, y), _map.MapId));
                    }
                }
            }

            return claimed;
        }

        private void UpdateOverlapWarnings()
        {
            var apcsByArea = new Dictionary<ushort, List<IAreaApcOrigin>>();

            foreach (IAreaApcOrigin apc in _registeredApcs)
            {
                if (!_registry.TryGetApcArea(apc, out AreaId areaId))
                    continue;

                if (!_map.TryGetAreaId(apc.OriginTile, out ushort originAreaId) || originAreaId == AreaId.None)
                    continue;

                if (!apcsByArea.TryGetValue(originAreaId, out List<IAreaApcOrigin> list))
                {
                    list = new List<IAreaApcOrigin>();
                    apcsByArea[originAreaId] = list;
                }

                list.Add(apc);
            }

            _overlapFlaggedApcs.Clear();

            foreach (List<IAreaApcOrigin> group in apcsByArea.Values)
            {
                if (group.Count <= 1)
                    continue;

                foreach (IAreaApcOrigin apc in group)
                    _overlapFlaggedApcs.Add(apc);
            }

            foreach (IAreaApcOrigin apc in _registeredApcs)
                apc.SetMultipleApcsInArea(_overlapFlaggedApcs.Contains(apc));

            if (IsServer)
            {
                UpdateAreaLightingStates();
            }
        }

        private void SubscribeElectricityTicks()
        {
            if (_electricityTickSubscribed || !SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                return;
            }

            electricitySubSystem.OnTick += HandleElectricityTick;
            _electricityTickSubscribed = true;
        }

        private void UnsubscribeElectricityTicks()
        {
            if (!_electricityTickSubscribed || !SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                return;
            }

            electricitySubSystem.OnTick -= HandleElectricityTick;
            _electricityTickSubscribed = false;
        }

        private void HandleElectricityTick()
        {
            if (!IsServer)
            {
                return;
            }

            UpdateAreaLightingStates();
        }

        private void UpdateAreaLightingStates(bool pushObservers = true)
        {
            if (!SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                return;
            }

            bool anyChanged = false;
            foreach (AreaRecord record in _registry.GetAllAreas())
            {
                if (record.Apc is not IApcChannelSource areaApc || record.Apc is not IElectricDevice)
                {
                    continue;
                }

                IPowerStorage apcStorage = record.Apc as IPowerStorage;
                if (!electricitySubSystem.TryGetApcCircuitStats(areaApc, apcStorage, out CircuitStats stats))
                {
                    continue;
                }

                AreaLightingState newState = AreaLightingStateDeriver.Derive(stats, areaApc.Channels, record.LightingSwitchOn);
                if (ApplyLightingStateChange(record.Id, newState))
                {
                    anyChanged = true;
                }
            }

            if (pushObservers && anyChanged && IsServer)
            {
                PushLightingSnapshotToObservers();
            }
        }

        private void ApplyLightingSwitchChange(AreaId areaId, bool on)
        {
            _lightingSwitchOn[areaId] = on;
            OnAreaLightingSwitchChanged?.Invoke(areaId, on);
        }

        /// <returns>True when the stored state changed.</returns>
        private bool ApplyLightingStateChange(AreaId areaId, AreaLightingState newState)
        {
            if (_lightingStates.TryGetValue(areaId, out AreaLightingState previousState) && previousState == newState)
            {
                return false;
            }

            _lightingStates[areaId] = newState;
            OnAreaLightingStateChanged?.Invoke(areaId, newState);
            return true;
        }

        [Server]
        private void PushLightingSnapshotToObservers()
        {
            var areaIds = new HashSet<AreaId>(_lightingStates.Keys);
            foreach (AreaId switchAreaId in _lightingSwitchOn.Keys)
            {
                areaIds.Add(switchAreaId);
            }

            var entries = new SyncedAreaLighting[areaIds.Count];
            int index = 0;
            foreach (AreaId areaId in areaIds)
            {
                if (!_lightingStates.TryGetValue(areaId, out AreaLightingState state))
                {
                    state = AreaLightingState.Dark;
                }

                if (!_lightingSwitchOn.TryGetValue(areaId, out bool switchOn))
                {
                    switchOn = true;
                }

                entries[index++] = new SyncedAreaLighting
                {
                    areaId = areaId.Value,
                    state = state,
                    lightingSwitchOn = switchOn,
                };
            }

            RpcSyncAreaLighting(entries);
        }

        /// <summary>
        /// Full lighting + wall-switch snapshot. BufferLast so late joiners get current fixture state
        /// (per-area RPCs would only retain the last area).
        /// </summary>
        [ObserversRpc(BufferLast = true)]
        private void RpcSyncAreaLighting(SyncedAreaLighting[] entries)
        {
            if (IsServer)
            {
                return;
            }

            var nextStates = new Dictionary<AreaId, AreaLightingState>();
            var nextSwitches = new Dictionary<AreaId, bool>();
            if (entries != null)
            {
                foreach (SyncedAreaLighting entry in entries)
                {
                    var areaId = new AreaId(entry.areaId);
                    nextStates[areaId] = entry.state;
                    nextSwitches[areaId] = entry.lightingSwitchOn;
                }
            }

            foreach (KeyValuePair<AreaId, AreaLightingState> pair in nextStates)
            {
                if (_lightingStates.TryGetValue(pair.Key, out AreaLightingState previous) && previous == pair.Value)
                {
                    continue;
                }

                _lightingStates[pair.Key] = pair.Value;
                OnAreaLightingStateChanged?.Invoke(pair.Key, pair.Value);
            }

            foreach (AreaId removed in _lightingStates.Keys.Where(id => !nextStates.ContainsKey(id)).ToList())
            {
                _lightingStates.Remove(removed);
            }

            foreach (KeyValuePair<AreaId, bool> pair in nextSwitches)
            {
                if (_lightingSwitchOn.TryGetValue(pair.Key, out bool previous) && previous == pair.Value)
                {
                    continue;
                }

                _lightingSwitchOn[pair.Key] = pair.Value;
                OnAreaLightingSwitchChanged?.Invoke(pair.Key, pair.Value);
            }

            foreach (AreaId removed in _lightingSwitchOn.Keys.Where(id => !nextSwitches.ContainsKey(id)).ToList())
            {
                _lightingSwitchOn.Remove(removed);
            }
        }

        private static void InvalidateElectricityConsumerIndex()
        {
            if (SubSystems.TryGet(out ElectricitySubSystem electricitySubSystem))
            {
                electricitySubSystem.InvalidateAreaConsumerIndex();
            }
        }

        [Server]
        private void NotifyAreaVisualsChanged()
        {
            if (_map == null)
                return;

            var tintList = new List<(ushort areaId, bool hasTint, Color tint)>();
            foreach (AreaRecord record in _registry.GetAllAreas())
            {
                tintList.Add((
                    record.Id.Value,
                    record.HasDepartmentalLightTint,
                    record.DepartmentalLightTint));
            }

            var chunkList = new List<(Vector2Int chunkKey, ushort[] areaIds)>();
            var rpcChunks = new List<SyncedChunkAreaIds>();
            foreach (TileChunk chunk in _map.GetAllChunks())
            {
                ushort[] areaIds = chunk.CopyAreaIds();
                if (areaIds == null)
                    continue;

                Vector2Int chunkKey = _map.GetKey(chunk.GetWorldPosition(0, 0));
                chunkList.Add((chunkKey, areaIds));
                rpcChunks.Add(new SyncedChunkAreaIds
                {
                    chunkKeyX = chunkKey.x,
                    chunkKeyY = chunkKey.y,
                    areaIds = areaIds,
                });
            }

            FloorVisualCache.ReplaceAll(tintList, chunkList);
            OnAreaVisualsDirty?.Invoke();

            var rpcTints = new SyncedAreaTint[tintList.Count];
            for (int i = 0; i < tintList.Count; i++)
            {
                (ushort areaId, bool hasTint, Color tint) = tintList[i];
                rpcTints[i] = new SyncedAreaTint
                {
                    areaId = areaId,
                    hasTint = hasTint,
                    tint = tint,
                };
            }

            RpcSyncAreaFloorVisuals(rpcTints, rpcChunks.ToArray());
        }

        [ObserversRpc(BufferLast = true)]
        private void RpcSyncAreaFloorVisuals(SyncedAreaTint[] tints, SyncedChunkAreaIds[] chunks)
        {
            if (IsServer)
                return;

            var tintList = new List<(ushort areaId, bool hasTint, Color tint)>();
            if (tints != null)
            {
                foreach (SyncedAreaTint tint in tints)
                    tintList.Add((tint.areaId, tint.hasTint, tint.tint));
            }

            var chunkList = new List<(Vector2Int chunkKey, ushort[] areaIds)>();
            if (chunks != null)
            {
                foreach (SyncedChunkAreaIds chunk in chunks)
                {
                    chunkList.Add((
                        new Vector2Int(chunk.chunkKeyX, chunk.chunkKeyY),
                        chunk.areaIds));
                }
            }

            FloorVisualCache.ReplaceAll(tintList, chunkList);
            OnAreaVisualsDirty?.Invoke();
        }

        [Serializable]
        private struct SyncedAreaTint
        {
            public ushort areaId;
            public bool hasTint;
            public Color tint;
        }

        [Serializable]
        private struct SyncedChunkAreaIds
        {
            public int chunkKeyX;
            public int chunkKeyY;
            public ushort[] areaIds;
        }

        [Serializable]
        private struct SyncedAreaLighting
        {
            public ushort areaId;
            public AreaLightingState state;
            public bool lightingSwitchOn;
        }
    }
}
