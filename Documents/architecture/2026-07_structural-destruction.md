> Implements: Documents/design/explosives-destruction.md §2 (blast BFS), §3 (structural stages), §4 (Area/atmos on Destroyed), §5 (crew brute)
> Touches systems: structural-destruction, tile, area, atmospherics, health
> Status: in-progress

# Structural destruction — integrity + blast

## Goal

Ship per-tile structural integrity for Turf walls/doors/windows and hop-based blast resolution that terminates in the same Apply path.

## Phase 1 shipped surface

- `StructuralDamageSubSystem` (self-bootstrap) + `StructuralDamageService.TryApplyStructuralDamage`
- Integrity SyncVars on `PlacedTileObject`; optional `TileObjectSo.structuralMaxIntegrity`
- Cracked → `TileOccupancy.IsAirtight = false` (wall still blocks Area expansion)
- Destroyed → `ConstructionService.TryClearTile` (debris spawn deferred)
- Area `OnTileCleared`/`OnTilePlaced` queues deferred `RefloodAllAreaTilesPreservingMetadata` (not yet true local-region flood)
- Console: `hurtstructure [force]`
- EditMode: `StructuralDamageTests`; Area door-clear live recompute harness test

## Phase 2 shipped surface

- `MeleeWeaponProfile.StructuralForce` + `ResolveStructuralForce()`
- `MeleeHitInteraction` connect: living first, else `MeleeStructuralHitResolver` → `TryApplyStructuralDamage`
- Crowbar 35 / hatchet 28 / fists 5 / knife 4 provisional force

## Phase 3 shipped surface

- `BlastResolutionService.Resolve(epicenter, yield, falloff)` — BFS, subtractive falloff, max-force revisit
- Blocked-edge apply to structural turf; Destroyed mid-pass cascades in the same resolve
- Crew chest brute on visited tiles (`EntitySubSystem.SpawnedPlayers` + `FindObjectsByType`)
- Console: `blast [yield] [falloff]` (defaults 120 / 25)
- EditMode: `BlastResolutionTests` (corridor, sealed door cascade, cracked wall blocks)

## Deviations / notes

- Phase 1 Area recompute is **full reflood preserving metadata**, deferred one Update tick because `TileMap` notifies clear **before** occupant removal (same pitfall as atmos). True local flood fill is follow-up.
- Debris/rubble spawn not wired (no art path yet).
- Blast hop rules duplicate atmos `CanFlow` bit checks locally (avoid `AtmosNeighbourBuilder` internal coupling).
- Items / visuals / repair: later phases in [station_structural_damage.plan.md](../plans/station_structural_damage.plan.md).

## Related

- Plan: [Documents/plans/station_structural_damage.plan.md](../plans/station_structural_damage.plan.md)
- Design: [Documents/design/explosives-destruction.md](../design/explosives-destruction.md)
- System map: [systems/structural-destruction.md](systems/structural-destruction.md)
