> Code paths: Assets/Scripts/SS3D/Systems/Substances/
> Entry points: SubstancesSubSystem, SubstanceContainer, ReactionResolver, TransferSubstanceInteraction, PourSubstanceInteraction
> Status: partial
> Verified: 3a8a57f65 — 2026-07-28

# Substances

## Overview

Volume-first liquid vessels with networked mixture entries, pour-triggered fixed-ratio reactions, and container temperature. Clean-slate rewrite per [substances-foundation](../2026-07_substances-foundation.md). Chemistry gameplay (analyzer knowledge, bloodstream, machines) is deferred to MVP2.

## Start here

- `Assets/Scripts/SS3D/Systems/Substances/SubstancesSubSystem.cs` — registry + resolver + temperature tick + hazard stub
- `Assets/Scripts/SS3D/Systems/Substances/SubstanceContainer.cs` — SyncList mixture; server-only `InitialMixture` seed
- `Assets/Scripts/SS3D/Systems/Substances/ReagentDefinition.cs` / `ReagentRegistry.cs` — shared reagent record (atmos §3 fields included)
- `Assets/Scripts/SS3D/Systems/Substances/ReactionResolver.cs` — indexed recipe match / near-miss / incompatible
- `Assets/Scripts/SS3D/Systems/Substances/Interactions/TransferSubstanceInteraction.cs` — Tier 2 armed transfer
- `Assets/Scripts/SS3D/Systems/Substances/Interactions/PourSubstanceInteraction.cs` — Tier 3 Combine pour
- `Assets/Scripts/SS3D/Systems/Substances/SubstanceContainerExaminable.cs` — free properties only (color / approx volume / temp band)
- `Assets/Content/Systems/Substances/CoreReagentRegistry.asset` — core reagents + vertical-slice recipes
- Editor: `SS3D/Substances/Run Content Prefab Recipes` (`SubstancesContentPrefabRecipes`)

## Extension points

- Add reagents/recipes via `ReagentRegistryGenerator` / registry asset; assign registry on hub `SubstancesSubSystem`.
- Wire hazard consumers to `SubstanceContainer.HazardOccurred` (stub logs today).
- Prefab consumers: register on `SubstancesContentPrefabSetup` / Human strip on `HumanPrefabRecipes`.

## Pitfalls

- **GC on mixture views:** do not call `List.AsReadOnly()` per hot read — container caches the view; mutators use `_working` / `_entries` directly.
- **Never seed `InitialMixture` from Unity `Start` on clients:** SyncVar/SyncList writes must be server-only (`OnStartServer`). Pure clients writing fail the smoke denylist.
- **Namespace is `SS3D.Systems.Substances`** (folder under `Systems/Substances/`). Legacy `SS3D.Substances` enum/SO stack is gone.

## Depends on / Used by

- **Depends on:** [interactions-framework](interactions-framework.md), [inventory](inventory.md), [atmospherics](atmospherics.md) (ambient T sample), [examine](examine.md)
- **Used by:** mug/soda/tank prefabs; chemistry gameplay (deferred MVP2). Health blood is `SystemicPools`, not this container. `GasDefinition.LinkedReagent` bridges atmos gases.

## Related docs

- Effort: [2026-07_substances-foundation.md](../2026-07_substances-foundation.md) — shipped
- Plan: [substances_foundation_implementation.plan.md](../../plans/substances_foundation_implementation.plan.md)
- Design (read-only): [Documents/design/chemistry.md](../../design/chemistry.md)
