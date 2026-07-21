> Code paths: Assets/Scripts/SS3D/Systems/StructuralDamage/, Assets/Scripts/SS3D/Systems/Tile/ (integrity stage + occupancy)
> Entry points: StructuralDamageSubSystem, StructuralDamageService, BlastResolutionService, HurtStructureCommand, BlastCommand
> Status: partial
> Verified: 418530c65 — 2026-07-21

# Structural destruction

## Overview

Per-tile integrity for Turf walls, doors, and windows per [explosives-destruction.md](../../design/explosives-destruction.md) §3. Phase 1: accumulate force → Damaged / Cracked / Destroyed; Destroyed clears via [tile](tile.md) `ConstructionService`; Cracked drops airtightness for [atmospherics](atmospherics.md); [area](area.md) refloods after structural place/clear. Phase 2: Harm melee connect applies `StructuralForce` when no living zone is hit ([combat](combat.md)). Phase 3: hop-based `BlastResolutionService` (yield + falloff) with cascade on Destroyed and provisional chest brute on visited tiles. Visuals, explosive items, and repair are later ([station_structural_damage.plan.md](../../plans/station_structural_damage.plan.md)).

## Start here

- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralDamageSubSystem.cs` — self-bootstrap; `TryApplyStructuralDamage` / `TryGetIntegrity` / `ResolveBlast`
- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralDamageService.cs` — server apply + Destroyed clear
- `Assets/Scripts/SS3D/Systems/StructuralDamage/BlastResolutionService.cs` — BFS blast spread
- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralIntegrityRules.cs` — provisional HP thresholds / stage fractions
- `Assets/Scripts/SS3D/Systems/Tile/StructuralIntegrityStage.cs` — Intact / Damaged / Cracked / Destroyed
- `Assets/Scripts/SS3D/Systems/Tile/PlacedObjects/PlacedTileObject.cs` — SyncVar integrity remaining + stage; `ServerSetIntegrity`
- `Assets/Scripts/SS3D/Systems/Tile/TileOccupancyEvaluator.cs` — Cracked → not airtight
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/HurtStructureCommand.cs` — admin `hurtstructure [force]`
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/BlastCommand.cs` — admin `blast [yield] [falloff]`
- `Assets/Scripts/Tests/EditMode/StructuralDamageTests.cs` — stage / airtight / clear tests
- `Assets/Scripts/Tests/EditMode/BlastResolutionTests.cs` — corridor / door cascade / cracked block

## Extension points

- Call `StructuralDamageSubSystem.TryApplyStructuralDamage(coord, force, source)` from melee, blast hops, chemistry, etc. — one apply path.
- Call `StructuralDamageSubSystem.ResolveBlast(epicenter, yield, falloff)` from grenades/charges (Phase 5) and other explosive sources.
- Override max HP per SO via `TileObjectSo.structuralMaxIntegrity` (> 0).

## Pitfalls

- **Area reflood must not run inside `OnTileCleared` synchronously:** `TileMap` notifies before the occupant is removed; immediate flood still sees the wall/door. Area queues `_pendingLiveBoundaryRecompute` and flushes next `UpdateEvent` (same class of bug as `AtmosTileObserver` defer).
- **Cracked still blocks Area expansion and blast hops:** only Destroyed (clear) opens boundaries; Cracked only flips `IsAirtight` for atmos.
- **Do not bind TileQueryService at Awake:** `StructuralDamageSubSystem` self-bootstraps before `TileSubSystem` creates its map. Resolve query/construction lazily from the current `TileSubSystem` or `TryApply` / `ResolveBlast` always miss.
- **Structural melee ray length ≠ hand range:** camera aim rays must cast ~8m (like living zones), then check `RangeLimit` from the **entity root** (not the swinging hand bone) to the hit/closest point. Hand-bone reach during windup often fails adjacent walls; cardinal-ahead is the last fallback.
- **Blast BFS keeps max force per tile:** weaker revisit paths are skipped; a stronger cascade path must still enqueue.
- **Blast hop checks mirror atmos `CanFlow` locally:** do not call `AtmosNeighbourBuilder` (`internal`); keep the BlockedEdges bit test in `BlastResolutionService`.

## Depends on / Used by

- **Depends on:** [tile](tile.md), [area](area.md), [atmospherics](atmospherics.md) (observer refresh), [health](health.md) (blast crew brute), [core-subsystems](core-subsystems.md)
- **Used by:** [combat](combat.md) melee connect; Phase 5+ explosive items

## Related docs

- Design: [Documents/design/explosives-destruction.md](../../design/explosives-destruction.md)
- Effort: [2026-07_structural-destruction.md](../2026-07_structural-destruction.md)
- Plan: [station_structural_damage.plan.md](../../plans/station_structural_damage.plan.md)
- [INDEX.md](../INDEX.md)
