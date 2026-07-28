> Code paths: Assets/Scripts/SS3D/Systems/Substances/
> Entry points: SubstancesSubSystem, SubstanceContainer, TransferSubstanceInteraction
> Status: partial
> Verified: 3db3babca — 2026-07-24

# Substances

## Overview

Chemical substances, containers, and transfer interactions. Current tree is **legacy** (contents not fully synced; recipes dead) pending clean-slate [substances-foundation](../2026-07_substances-foundation.md). `TransferSubstanceInteraction` remains the Tier 2 armed proof-of-concept for [interactions-runtime](interactions-runtime.md). Keep container hot paths allocation-free (`AsReadOnly` pitfall below).

## Start here

- `Assets/Scripts/SS3D/Systems/Substances/SubstancesSubSystem.cs` — subsystem entry point
- `Assets/Scripts/SS3D/Systems/Substances/SubstanceContainer.cs` — networked substance storage; caches `AsReadOnly()` view once
- `Assets/Scripts/SS3D/Systems/Substances/Interactions/TransferSubstanceInteraction.cs` — armed transfer (`ITargetedInteraction`, wire ID `TransferSubstance`)

## Extension points

- Add substance interactions via `IInteraction` on containers or tools; use `IInteractionTierProvider` when radial tier is not instant.
- Targeted transfers: implement `ITargetedInteraction.CanTarget` for origin→target validation.

## Pitfalls

- **GC on `SubstanceContainer.Substances`:** `List.AsReadOnly()` allocates a new wrapper every call. Cache the view; internal mutators (`IndexOfSubstance`, `RemoveSubstance`, volume recalcs) must use `_substances` directly. Heart bleed previously spiked GC through this property.
- **Never seed `InitialSubstances` from Unity `Start` on clients:** `AddSubstance` → `_currentVolume` SyncVar. Pure clients writing it fail the smoke denylist. Use FishNet `OnStartServer` (clients receive contents via SyncVars). Hit: `OxygenTank(Clone)` / `SubstanceContainer` after empty map create.

## Depends on / Used by

- **Depends on:** [interactions-framework](interactions-framework.md), [inventory](inventory.md)
- **Used by:** gas/drink tank prefabs (temporary); chemistry gameplay (deferred MVP2). Health blood is `SystemicPools`, not this container.

## Related docs

- Effort (planned): [2026-07_substances-foundation.md](../2026-07_substances-foundation.md) — clean-slate Phases 0–2; chemistry gameplay deferred to MVP2
- Plan: [radial_menu_implementation_5a83bdf9.plan.md](../../plans/radial_menu_implementation_5a83bdf9.plan.md) § Phase 3
- Design (read-only): [Documents/design/chemistry.md](../../design/chemistry.md)
