> Code paths: Assets/Scripts/SS3D/Systems/StructuralDamage/, Assets/Scripts/SS3D/Systems/Tile/ (integrity stage + occupancy + blast VFX Rpc)
> Entry points: StructuralDamageSubSystem, StructuralDamageService, BlastResolutionService, BlastVfxPresenter, StructuralIntegrityPresenter, HurtStructureCommand, BlastCommand
> Status: partial
> Verified: 82c1fed63 — 2026-07-21

# Structural destruction

## Overview

Per-tile integrity for Turf walls, doors, and windows per [explosives-destruction.md](../../design/explosives-destruction.md) §3. Phase 1–3: accumulate force → stages → Destroyed clear; melee StructuralForce; hop blast BFS + crew brute. Phase 4: wall stage MPB tint + Cracked hiss + examine. Blast detonation VFX: epicenter fireball wash (atmos-fire palette), point light, boom SFX, scorch decals, distance-scaled shake + blast screen flash — via `TileSubSystem` ObserversRpc. Explosive items and repair remaining: [station_structural_damage.plan.md](../../plans/station_structural_damage.plan.md).

## Start here

- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralDamageSubSystem.cs` — self-bootstrap; `TryApplyStructuralDamage` / `TryGetIntegrity` / `ResolveBlast` (+ VFX broadcast)
- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralDamageService.cs` — server apply + Destroyed clear
- `Assets/Scripts/SS3D/Systems/StructuralDamage/BlastResolutionService.cs` — BFS blast spread
- `Assets/Scripts/SS3D/Systems/StructuralDamage/BlastVfxPresenter.cs` — client detonation FX (catalog-driven)
- `Assets/Scripts/SS3D/Systems/StructuralDamage/BlastVfxCatalog.cs` — Resources catalog + falloff tuning
- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralIntegrityPresenter.cs` — Damaged/Cracked MPB tint + optional WindLight hiss
- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralIntegrityRules.cs` — provisional HP thresholds / stage fractions
- `Assets/Scripts/SS3D/Systems/Tile/StructuralIntegrityStage.cs` — Intact / Damaged / Cracked / Destroyed
- `Assets/Scripts/SS3D/Systems/Tile/PlacedObjects/PlacedTileObject.cs` — SyncVar integrity + `ServerSetIntegrity` → presenter
- `Assets/Scripts/SS3D/Systems/Tile/TileSubSystem.cs` — `ServerNotifyBlastDetonated` / `RpcBlastDetonated`
- `Assets/Scripts/SS3D/Systems/Examine/StructuralIntegrityExaminable.cs` — Damaged/Cracked examine sections
- `Assets/Scripts/SS3D/Systems/Tile/TileOccupancyEvaluator.cs` — Cracked → not airtight
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/HurtStructureCommand.cs` — admin `hurtstructure [force]`
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/BlastCommand.cs` — admin `blast [yield] [falloff]`
- `Assets/Scripts/SS3D/Systems/StructuralDamage/Editor/StructuralIntegrityPrefabSetup.cs` — wall/window presenter + examinable
- `Assets/Scripts/SS3D/Systems/StructuralDamage/Editor/BlastVfxSetup.cs` — blast catalog/prefab/scorch assets
- `Assets/Scripts/Tests/EditMode/StructuralDamageTests.cs` / `BlastResolutionTests.cs` / `StructuralIntegrityPresentationTests.cs` / `BlastVfxFalloffTests.cs`

## Extension points

- Call `StructuralDamageSubSystem.TryApplyStructuralDamage(coord, force, source)` from melee, blast hops, chemistry, etc. — one apply path.
- Call `StructuralDamageSubSystem.ResolveBlast(epicenter, yield, falloff)` from grenades/charges (Phase 5) — also broadcasts blast VFX.
- Override max HP per SO via `TileObjectSo.structuralMaxIntegrity` (> 0).
- Wall/window prefabs: **SS3D → Structural Damage → Setup Wall Integrity Presentation**.
- Blast assets: **SS3D → Structural Damage → Setup Blast VFX Assets**; assign boom `AudioClip` on `BlastVfxCatalog`.

## Pitfalls

- **Area reflood must not run inside `OnTileCleared` synchronously:** `TileMap` notifies before the occupant is removed; immediate flood still sees the wall/door. Area queues `_pendingLiveBoundaryRecompute` and flushes next `UpdateEvent` (same class of bug as `AtmosTileObserver` defer).
- **Cracked still blocks Area expansion and blast hops:** only Destroyed (clear) opens boundaries; Cracked only flips `IsAirtight` for atmos.
- **Cracked is not a slow room↔room leak yet:** design wants a slow vent; `IsAirtight=false` only changes the wall cell's atmos state. Neighbour `CanFlow` still uses binary `BlockedEdges` — real permeability deferred (document fork until ShareGasJob / edge rate exists).
- **Do not bind TileQueryService at Awake:** `StructuralDamageSubSystem` self-bootstraps before `TileSubSystem` creates its map. Resolve query/construction lazily from the current `TileSubSystem` or `TryApply` / `ResolveBlast` always miss.
- **Structural melee ray length ≠ hand range:** camera aim rays must cast ~8m (like living zones), then check `RangeLimit` from the **entity root** (not the swinging hand bone) to the hit/closest point. Hand-bone reach during windup often fails adjacent walls; cardinal-ahead is the last fallback.
- **Blast BFS keeps max force per tile:** weaker revisit paths are skipped; a stronger cascade path must still enqueue.
- **Blast hop checks mirror atmos `CanFlow` locally:** do not call `AtmosNeighbourBuilder` (`internal`); keep the BlockedEdges bit test in `BlastResolutionService`.
- **Never assign integrity SyncVars without a spawned NetworkObject:** FishNet `SyncBase.IsNetworkInitialized` NREs when `_networkObjectCache` is null (common for door/floor prefabs that get `PlacedTileObject` via `AddComponent` at place time). `ServerSetIntegrity` falls back to local fields when the NB cache is missing or not spawned (same guard pattern as `SetDirection`). Server damage/clear still works; clients will not see stage SyncVars on those tiles until prefabs bake `PlacedTileObject`.
- **Host SyncVar OnChange may skip:** `ServerSetIntegrity` always calls `StructuralIntegrityPresenter.Apply` so host tint/hiss updates without relying on FishNet OnChange.
- **Blast VFX Rpc must RunLocally and must not BufferLast:** one-shot boom; host needs `RunLocally = true`. Scorch decal materials stay **Opaque** (same URP Decal pitfall as blood).

## Depends on / Used by

- **Depends on:** [tile](tile.md), [area](area.md), [atmospherics](atmospherics.md) (observer refresh), [health](health.md) (blast crew brute), [examine](examine.md), [screen-effects](screen-effects.md) (blast flash), [core-subsystems](core-subsystems.md)
- **Used by:** [combat](combat.md) melee connect; Phase 5+ explosive items

## Related docs

- Design: [Documents/design/explosives-destruction.md](../../design/explosives-destruction.md)
- Effort: [2026-07_structural-destruction.md](../2026-07_structural-destruction.md)
- Plan: [station_structural_damage.plan.md](../../plans/station_structural_damage.plan.md)
- [INDEX.md](../INDEX.md)
