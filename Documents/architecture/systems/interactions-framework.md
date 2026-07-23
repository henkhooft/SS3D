> Code paths: Assets/Scripts/SS3D/Interactions/
> Entry points: IInteraction, IInteractionSource, IInteractionTarget, InteractionPipeline, InteractionIdentifier
> Status: shipped
> Verified: 011659a01 — 2026-07-23

# Interactions (framework)

## Overview

Shared interaction contracts used across all gameplay systems. Defines how interaction sources discover targets, build `InteractionEntry` lists, and route client/server execution. Domain systems implement `IInteraction` on behaviours; runtime routing lives in [interactions-runtime](interactions-runtime.md).

RPCs identify interactions with `InteractionIdentifier` (`genericName` + `targetComponentIndex`), never display `GetName()`.

**Discover contract** (TECH_DEBT 1.3 / [2026-07_interaction-discover-contract](../2026-07_interaction-discover-contract.md)): Discover produces **candidates**; `FilterAndSort` is the sole full viability gate for menus/RPC. Target-bound entries have `Target != null`; source-only (e.g. Drop) use `InteractionEntry.SourceOnly` (`IsSourceOnly`, wire index `-1`). `InteractionEvent.HasPoint` is explicit — never treat `Point == zero` as unset.

## Start here

- `Assets/Scripts/SS3D/Interactions/Interfaces/IInteraction.cs` — core contract (`GetGenericName`, `Priority`, server-only `Start`)
- `Assets/Scripts/SS3D/Interactions/Interfaces/IInteractionSource.cs` — objects that offer interactions (hands, items); `CreateSourceInteractions(..., context)`
- `Assets/Scripts/SS3D/Interactions/Interfaces/IInteractionTarget.cs` — objects that receive interactions
- `Assets/Scripts/SS3D/Interactions/Interfaces/IInteractionSourceExtension.cs` — source extensions; Discover contract in XML remarks
- `Assets/Scripts/SS3D/Interactions/InteractionEntry.cs` — target + interaction + wire identifier; `IsSourceOnly` / `SourceOnly()`
- `Assets/Scripts/SS3D/Interactions/InteractionIdentifier.cs` — stable RPC wire ID
- `Assets/Scripts/SS3D/Interactions/InteractionPipeline.cs` — discover → filter → sort; `TryEvaluateOutlineInteractability`; `FilterForOutline`
- `Assets/Scripts/SS3D/Interactions/InteractionEvent.cs` — source/target/`HasPoint`/point/normal; `WithTarget` / `WithSource`
- `Assets/Scripts/SS3D/Interactions/InteractionTier.cs` — instant / targeted / folder tiers for radial menu
- `Assets/Scripts/SS3D/Interactions/Interfaces/IInteractionTierProvider.cs` — per-interaction tier override
- `Assets/Scripts/SS3D/Interactions/Extensions/InteractionExtensions.cs` — `RangeCheck` (uses `HasPoint`), `GetInteractionTier()`
- `Assets/Scripts/SS3D/Interactions/InteractionOptimisticFeedback.cs` — delayed loading bars during server confirm
- `Assets/Scripts/SS3D/Interactions/Interfaces/IIntentRestrictedInteraction.cs` — Help/Harm gate (unrestricted = Help-default; Harm must opt in)
- `Assets/Scripts/SS3D/Interactions/Interfaces/ITargetedInteraction.cs` — armed-mode second-click targeting
- `Assets/Scripts/SS3D/Interactions/DelayedInteraction.cs` — timed interaction base class
- `Assets/Scripts/SS3D/Interactions/InteractionIconLookup.cs` — resolves radial/menu sprites from `InteractionIcons` (`AddressablesAsync` DB; sync Get needs warmup — [data-codegen](data-codegen.md) pitfalls)

## Extension points

- Implement `IInteraction` (or subclass `DelayedInteraction`) on a `NetworkBehaviour` for new interaction types.
- Add `InteractionTargetBehaviour` (or `InteractionTargetNetworkBehaviour`) to world objects that should receive interactions.
- Implement `IInteractionTierProvider` to control radial menu tier (instant vs armed targeted).
- Use `Requirement` and `IInteractionRangeLimit` / `RangeLimit` for gating.
- Register interaction icons via generated `InteractionIcons` asset refs ([data-codegen](data-codegen.md)); that DB is `AddressablesAsync` (warm-preloaded). Expose named helpers on `InteractionIconLookup` when shared. Do not add another one-shot icon rebuild `MenuItem` — see [data-codegen](data-codegen.md) § Architecture smells.
- Replicated state changes in `Start()` must go through networked components (`NetworkedOpenable.SetOpenState`, `SyncVar` toggles), not local-only animator writes.
- Source extensions: gate target-bound Adds (structural and/or `CanInteract`); never unconditional Add for every hover. Source-only verbs: `InteractionEntry.SourceOnly` once per Discover. Prefer `context.WithTarget(target)` so `HasPoint` is preserved.

## Architecture smells

1. **Pickable ≠ rangeable** (owned with [selection](selection.md)): shader pick works without colliders; range/drop need a resolved point from colliders. Missing colliders on `Selectable` wall mounts still force `HasPoint == false` and transform fallback — ship a `BoxCollider` for selection rays.

Resolved by [2026-07_interaction-discover-contract](../2026-07_interaction-discover-contract.md): Discover semantics / source-only typing / `HasPoint` (former smells #1–3).

## Pitfalls

- **Source-only vs outline:** Drop is `IsSourceOnly`. Outline LateUpdate must use `TryEvaluateOutlineInteractability` (skips source discovery) or `FilterForOutline` — never treat full Discover as “available on this hover.”
- **Unresolved point:** build events with the two-arg ctor (`HasPoint = false`), not `Point = Vector3.zero`. World-origin hits use the four-arg ctor with `Point = zero` and `HasPoint = true`.
- **Unconditional source `Add` pollutes Discover:** any extension that adds for every target lights yellow outlines / menus on every hover when that source is active. Gate at discover time. Historical example: empty-hand `Craft` → `OpenCraftingMenu` (purged with [crafting](crafting.md) / TECH_DEBT 1.6).

## Depends on / Used by

- **Used by:** [interactions-runtime](interactions-runtime.md), [inventory](inventory.md), [furniture](furniture.md), [tile](tile.md), and most gameplay systems
- **Depends on:** [core-subsystems](core-subsystems.md) (network actors)

## Related docs

- Effort: [2026-07_interaction-discover-contract](../2026-07_interaction-discover-contract.md)
- Effort: [2026-07_interaction-system-hardening](../2026-07_interaction-system-hardening.md)
- Plan: [interaction_system_improvements_9e14ae22.plan.md](../../plans/interaction_system_improvements_9e14ae22.plan.md)
- Plan: [radial_menu_implementation_5a83bdf9.plan.md](../../plans/radial_menu_implementation_5a83bdf9.plan.md)
- Design (read-only): [Documents/design/main-hud.md](../../design/main-hud.md) § intent chording
- Tests: `Assets/Scripts/Tests/EditMode/InteractionPipelineTests.cs`, `InteractionRangeCheckTests.cs`
