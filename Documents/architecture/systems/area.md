> Code paths: Assets/Scripts/SS3D/Systems/Area/
> Entry points: AreaSubSystem, AreaFloodFillService, AreaBoundaryEvaluator
> Status: partial
> Verified: a8624d365 — 2026-07-29 (sticky None resolve; area membership index invalidate)

# Area

## Overview

APC-seeded area flood-fill: each APC owns one `AreaRecord` and claims reachable floor tiles with a per-chunk `ushort[]` area-id layer. Walls and doors block expansion; unclaimed tiles stay `AreaId.None`. Live boundary recompute on tile mutation is deferred — rebuild runs on map load and APC place/remove only. Area metadata persists via [persistence](persistence.md) `AreaPersistenceContributor`; template restore uses `BeginTemplateRestore` / `RestoreFromSave` / `EndTemplateRestore` so saved display names, tints, access bits, and `lightingSwitchOn` survive when APCs are already placed.

Per-consumer power gating and **area-scoped APC cell drain** via [electricity](electricity.md) `AreaApcPowerDistribution`: devices in an assigned area use their area APC's channels and cell, not circuit-wide OR or equal battery split. `AreaLightingState` (Normal/Emergency/Dark) is derived each electricity tick from the area APC's circuit stats and `AreaRecord.LightingSwitchOn`; a disabled lighting channel forces Dark regardless of cell charge. Transitions fire `OnAreaLightingStateChanged` and sync to clients via a **BufferLast full lighting snapshot** (`RpcSyncAreaLighting`) — not per-area RPCs (those only retain the last area for late joiners). Wall `LightSwitchController` toggles `LightingSwitchOn` for its area. `LightPower` consumes area state for fixture on/off/emergency visuals; optional `DepartmentalLightTint` on `AreaRecord` tints normal-mode emission **and** drives client floor-corner stripes via `AreaFloorStripeView` (area-id grids + tint snapshot synced to observers). Pure clients have no flood-fill registry: fixture/switch code resolves area ids via `TryResolveAreaIdForDevice` → `AreaFloorVisualCache`.

**Fork deviations from** [area.md](../../design/area.md): areas are APC-seeded (not generic auto-detection); unclaimed tiles have no fallback area; all doors block expansion regardless of open/closed state. Wall-mounted APCs seed flood fill from the walkable tile **in front of** `FacingDirection`, not from every cardinal neighbor. Live boundary recompute on structural Turf place/clear is **queued** (next Update) then full `RefloodAllAreaTilesPreservingMetadata` — not yet a true local-region flood ([structural-destruction](structural-destruction.md)).

## Start here

- `Assets/Scripts/SS3D/Systems/Area/AreaSubSystem.cs` — registry, APC lifecycle, rebuild; `IWorldReady`; notifies `AreasFlooded` after deferred flood; `TryResolveAreaIdForDevice` / lighting snapshot sync
- `Assets/Scripts/SS3D/Systems/Area/AreaFloodFillService.cs` — BFS from APC seeds, door-tile post-pass
- `Assets/Scripts/SS3D/Systems/Area/AreaBoundaryEvaluator.cs` — walkability and expansion blocking rules
- `Assets/Scripts/SS3D/Systems/Area/AreaRegistry.cs` — `AreaRecord` storage and APC reverse lookup
- `Assets/Scripts/SS3D/Systems/Area/IAreaApcOrigin.cs` — APC contract (`OriginTile`, `FacingDirection`)
- `Assets/Scripts/SS3D/Systems/Area/AreaLightingStateDeriver.cs` — Normal/Emergency/Dark from `CircuitStats` + switch state
- `Assets/Scripts/SS3D/Systems/Area/LightSwitchController.cs` — wall switch interaction, power consumer, area lighting toggle
- `Assets/Scripts/SS3D/Systems/Area/IAreaLightingStateSource.cs` — lighting state query contract
- `Assets/Scripts/SS3D/Systems/Area/AreaLightFixturePolicy.cs` — fixture emit policy (Normal/Emergency/Dark)
- `Assets/Scripts/SS3D/Systems/Area/LightFixtureCapability.cs` — `NormalOnly` / `EmergencyCapable` fixture tag
- `Assets/Scripts/SS3D/Systems/Area/AreaFloorStripeView.cs` — client mesh floor corners from departmental tint (HashGrid AOI chunk cull via `TileAoiVisibility`)
- `Assets/Scripts/SS3D/Systems/Area/AreaFloorVisualCache.cs` — host/client cache of areaIds + tints; `TryGetAreaIdForWorldGrid`
- `Assets/Scripts/SS3D/Systems/Area/AreaDeviceTileResolver.cs` — wall-mount front-tile + floor-cache area resolve
- `Assets/Scripts/SS3D/Systems/Area/AreaDevSettings.cs` — dev toggle (`SS3D → Dev → Areas → Show Area Gizmos`)
- `Assets/Scripts/SS3D/Systems/Area/AreaDebugGizmoDrawer.cs` — Scene-view tile overlay, APC labels (host/server map only)
- `Assets/Scripts/Tests/EditMode/AreaFloodFillTests.cs` — flood-fill and boundary edit-mode tests
- `Assets/Scripts/Tests/EditMode/Area/AreaFloorVisualCacheTests.cs` — client cache world-grid lookup
- `Assets/Scripts/Tests/EditMode/ElectricityTests/AreaLightFixturePolicyTests.cs` — fixture policy tests
- `Assets/Scripts/Tests/EditMode/ElectricityTests/AreaLightingStateDeriverTests.cs` — lighting state + wall-switch-off → Dark

## Extension points

- Resolve area for a tile: `AreaSubSystem.TryGetAreaForTile` / `ITileQueryService.TryGetAreaId`.
- Resolve area for wall-mounted devices: `AreaSubSystem.TryGetAreaForDevice` (tile in front of `Direction`).
- Resolve area record by id: `AreaSubSystem.TryGetArea`.
- Client / no-registry area id: `TryResolveAreaIdForDevice` — server/host with live map uses `TryGetAreaForDevice` only (sticky None); pure clients use `FloorVisualCache`.
- Register APC origins: implement `IAreaApcOrigin` (see `ApcController`).
- Server rename/tag API: `AreaSubSystem.RenameArea`, `SetParentTag` (no editor UI yet).
- Resolve effective APC for a device: `AreaSubSystem.TryGetEffectiveApcForDevice`.
- Area rebuild / APC lifecycle invalidates electricity's per-APC consumer index and atmos's area→port index via `InvalidateAreaMembershipIndexes`.
- Query lighting by tile: `IAreaLightingStateSource.TryGetLightingStateForTile`.
- Subscribe to area lighting transitions: `OnAreaLightingStateChanged` (do **not** gate on obsolete `IsSetUp` — use `IWorldReady` / lighting snapshot; pure clients never flood).
- World readiness: after `EndDeferredAreaFlood`, notify `WorldReadinessSubSystem.NotifyAreasFlooded` — consumers await `AreasFlooded` / `WorldReady`, not `OnMapCreated`.
- Toggle area fixture lighting: `ToggleAreaLightingSwitch` via `LightSwitchController` (separate from APC lighting breaker in MI).
- Subscribe to wall-switch changes: `OnAreaLightingSwitchChanged`.
- Departmental tint API: `SetDepartmentalLightTint` / `ClearDepartmentalLightTint` (server); clients read via `TryGetDepartmentalLightTint`.
- Ambience track API ([audio-foundation](../2026-07_audio-foundation.md) Phase 2): `SetAreaAmbienceTrackId` (server, no Map Editor UI yet — reachable via the `areaambience` dev console command, [audio](audio.md)) sets `AreaRecord.AmbienceTrackId`; `TryGetAmbienceTrackId` reads live registry (host) or the synced `RpcSyncAreaAmbience` snapshot (clients), same two-tier pattern as tint. **Not persisted** — `AmbienceTrackId` is absent from `SavedAreaRecord`/`BuildSavedAreaRecords`/`RestoreFromSave`, so it resets on map reload until a persistence contributor is added.
- Client-safe world-position → area id: `TryResolveAreaIdForWorldPosition` (live registry, else `FloorVisualCache` via the same world-grid math `ITileQueryService.WorldToTile` uses) — for presentation that tracks a moving position (e.g. ambience) rather than a fixed device tile.
- Fixture visuals: `LightPower` + `AreaLightFixturePolicy` + `LightFixtureCapability` on prefabs.
- Dev bypass (`SS3D → Dev → Lighting → Always Power Light Fixtures`) treats fixtures as powered but still respects APC channels and area Normal/Emergency/Dark policy.
- Template restore: `BeginTemplateRestore` → `RestoreFromSave` → APC registration → `EndTemplateRestore`.
- **Not yet wired:** fixture subset authoring on `AreaRecord`.

## Pitfalls

- **Floor stripes/decals drew the whole station:** `AreaFloorStripeView` / `FloorDecalView` used to spawn quads for every chunk. They now cull via `TileAoiVisibility` (Map Editor still shows all). Hit 2026-07-28 (Metastation).
- **APC area only fills front/right at game start, left empty until remove/re-add:** `ApcController.OnStartServer` → `RegisterApc` → flood runs during `TileMap.Load` while later chunks are still unplaced. Missing plenums look unwalkable, so BFS never claims that side; live mutation rebuild is deferred. Fix: `PersistenceSubSystem` / legacy `TileSubSystem.Load` wrap load in `BeginDeferredAreaFlood` / `EndDeferredAreaFlood` (refloods after the full map exists, preserving AreaRecord metadata). Do not flood from `RegisterApc` while deferred. Tests: `DeferredFlood_*`, `FloodWithoutDefer_OnIncompleteMap_MissesUnplacedWestTiles`.
- **Live structural clear must not reflood inside `OnTileCleared`:** `TileMap` notifies before occupant removal. `AreaSubSystem` sets `_pendingLiveBoundaryRecompute` and flushes on next `UpdateEvent` ([structural-destruction](structural-destruction.md)).
- **Light switch usable from across the room:** prefab had no collider, selection never resolved a point, and `RangeCheck` treated zero point as unlimited — see [interactions-framework](interactions-framework.md) Pitfalls. LightSwitch now has a BoxCollider; RangeCheck falls back to target transform.
- **Client fixtures stay stuck on/off (host OK):** Host fixture logic can read the area APC; pure clients cannot. `LightPower` is a `NetworkActor` with SyncVar `_fixtureVisual` — server computes Off/Normal/Emergency, clients only apply. Do not gate client visuals on obsolete `IsSetUp` or re-derive emit from floor-cache alone.
- **Act before flood:** registration ≠ readiness — await `WorldReadyPhase.AreasFlooded` (see [core-subsystems](core-subsystems.md)).
- **Device→area resolve must not re-walk the map every call:** `TryGetAreaForDevice` builds `TileCoord`s and walks the registry. Electricity used to call it twice per consumer per tick — Metastation deep profiles showed ~12k `TryGetAreaId`/inflated-frame. Prefer mutation-driven indexes (electricity APC→consumers, atmos area→ports) for membership lists; the StructureVersion device cache is a sticky resolve for incidental callers, cleared on reflood / structure bump. Hit 2026-07-29.
- **`TryResolveAreaIdForDevice` must not fall through to floor-cache on server miss:** live `TryGetAreaForDevice` already sticky-caches None. Falling through made unassigned devices pay registry + floor-cache forever. Server/host with `_map` returns false on miss; pure clients still use `FloorVisualCache`. Hit 2026-07-29.

## Depends on / Used by

- **Depends on:** [tile](tile.md) (`TileMap` area-id storage, `ITileQueryService`), [persistence](persistence.md) (area contributor chunk, `OnAfterRestore` lifecycle)
- **Used by:** [machine-interface](machine-interface.md) (APC overlap diagnostic); [electricity](electricity.md) (area→APC resolver, `LightPower` fixture visuals, `LightSwitchController` consumer); [persistence](persistence.md) (area metadata capture/restore); [audio](audio.md) (`AmbienceSubSystem` per-area crossfade via `TryResolveAreaIdForWorldPosition` / `TryGetAmbienceTrackId`)

## Related docs

- Plan: [areas_implementation_plan_c0639343.plan.md](../../plans/areas_implementation_plan_c0639343.plan.md)
- Plan: [persistence_architecture_design_2fe61864.plan.md](../../plans/persistence_architecture_design_2fe61864.plan.md)
- Architecture effort: [2026-07_area-foundation](../2026-07_area-foundation.md)
- Architecture effort: [2026-07_mi-area-electricity-debt](../2026-07_mi-area-electricity-debt.md)
- Architecture effort: [2026-07_audio-foundation](../2026-07_audio-foundation.md) (Phase 2 ambience track sync)
- Effort: [2026-07_tile-overlay-replacement](../2026-07_tile-overlay-replacement.md)
- Effort: [2026-07_session-world-lifecycle](../2026-07_session-world-lifecycle.md)
- Related docs: [structural-destruction](structural-destruction.md)
- Design (read-only): [Documents/design/area.md](../../design/area.md)
