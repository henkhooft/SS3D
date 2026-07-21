> Implements: Documents/design/explosives-destruction.md §3 (structural stages), §4 (Area/atmos on Destroyed)
> Touches systems: structural-destruction, tile, area, atmospherics
> Status: in-progress

# Structural destruction — Phase 1 integrity core

## Goal

Ship per-tile structural integrity for Turf walls/doors/windows: Intact → Damaged → Cracked/venting → Destroyed, with Destroyed clearing the tile and Area live boundary recompute.

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

## Deviations / notes

- Phase 1 Area recompute is **full reflood preserving metadata**, deferred one Update tick because `TileMap` notifies clear **before** occupant removal (same pitfall as atmos). True local flood fill is follow-up.
- Debris/rubble spawn not wired (no art path yet).
- Melee/blast/items/repair: later phases in [station_structural_damage.plan.md](../plans/station_structural_damage.plan.md).

## Related

- Plan: [Documents/plans/station_structural_damage.plan.md](../plans/station_structural_damage.plan.md)
- Design: [Documents/design/explosives-destruction.md](../design/explosives-destruction.md)
- System map: [systems/structural-destruction.md](systems/structural-destruction.md)
