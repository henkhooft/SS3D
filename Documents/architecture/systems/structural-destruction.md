> Code paths: Assets/Scripts/SS3D/Systems/StructuralDamage/, Assets/Scripts/SS3D/Systems/Tile/ (integrity stage + occupancy)
> Entry points: StructuralDamageSubSystem, StructuralDamageService, HurtStructureCommand
> Status: partial
> Verified: 8c7770be3 — 2026-07-21

# Structural destruction

## Overview

Per-tile integrity for Turf walls, doors, and windows per [explosives-destruction.md](../../design/explosives-destruction.md) §3. Phase 1: accumulate force → Damaged / Cracked / Destroyed; Destroyed clears via [tile](tile.md) `ConstructionService`; Cracked drops airtightness for [atmospherics](atmospherics.md); [area](area.md) refloods after structural place/clear. Blast BFS, melee connect, visuals, explosive items, and repair are later phases ([station_structural_damage.plan.md](../../plans/station_structural_damage.plan.md)).

## Start here

- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralDamageSubSystem.cs` — self-bootstrap; `TryApplyStructuralDamage` / `TryGetIntegrity`
- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralDamageService.cs` — server apply + Destroyed clear
- `Assets/Scripts/SS3D/Systems/StructuralDamage/StructuralIntegrityRules.cs` — provisional HP thresholds / stage fractions
- `Assets/Scripts/SS3D/Systems/Tile/StructuralIntegrityStage.cs` — Intact / Damaged / Cracked / Destroyed
- `Assets/Scripts/SS3D/Systems/Tile/PlacedObjects/PlacedTileObject.cs` — SyncVar integrity remaining + stage; `ServerSetIntegrity`
- `Assets/Scripts/SS3D/Systems/Tile/TileOccupancyEvaluator.cs` — Cracked → not airtight
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/HurtStructureCommand.cs` — admin `hurtstructure [force]`
- `Assets/Scripts/Tests/EditMode/StructuralDamageTests.cs` — stage / airtight / clear tests

## Extension points

- Call `StructuralDamageSubSystem.TryApplyStructuralDamage(coord, force, source)` from melee, blast, chemistry, etc. — one apply path.
- Override max HP per SO via `TileObjectSo.structuralMaxIntegrity` (> 0).

## Pitfalls

- **Area reflood must not run inside `OnTileCleared` synchronously:** `TileMap` notifies before the occupant is removed; immediate flood still sees the wall/door. Area queues `_pendingLiveBoundaryRecompute` and flushes next `UpdateEvent` (same class of bug as `AtmosTileObserver` defer).
- **Cracked still blocks Area expansion:** only Destroyed (clear) opens boundaries; Cracked only flips `IsAirtight` for atmos.
- **Do not bind TileQueryService at Awake:** `StructuralDamageSubSystem` self-bootstraps before `TileSubSystem` creates its map. Resolve query/construction lazily from the current `TileSubSystem` or `TryApply` always misses.

## Depends on / Used by

- **Depends on:** [tile](tile.md), [area](area.md), [atmospherics](atmospherics.md) (observer refresh), [core-subsystems](core-subsystems.md)
- **Used by:** (Phase 2+) combat melee; (Phase 3+) blast resolver

## Related docs

- Design: [Documents/design/explosives-destruction.md](../../design/explosives-destruction.md)
- Effort: [2026-07_structural-destruction.md](../2026-07_structural-destruction.md)
- Plan: [station_structural_damage.plan.md](../../plans/station_structural_damage.plan.md)
- [INDEX.md](../INDEX.md)
