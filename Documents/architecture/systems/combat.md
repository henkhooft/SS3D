> Code paths: Assets/Scripts/SS3D/Systems/Combat/, Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/
> Entry points: (condemned) MeleeHitInteraction / HandHit / MeleeWeaponItemExtension; keep HumanoidCombatController
> Status: condemned
> Verified: 94d038384 — 2026-07-19

# Combat

## Overview

**Obsolete — due for removal**, not extension. The Phase 4 melee vertical slice under
`Assets/Scripts/SS3D/Systems/Combat/` is a disconnected prototype: melee-stance LMB plays
swing animation without running hit resolution; fists (`HandHit`) were never prefab-wired;
windup/recovery data is not married to the swing telegraph. Do not add weapons, profiles, or
hit paths here.

Clean-slate rebuild is specified in [combat_implementation_plan.md](../../plans/combat_implementation_plan.md)
(Phase 0 purge → Phase 1 unified melee). Design authority: [combat.md](../../design/combat.md).

**Keep (non-Combat):** health zone damage APIs (`ApplyDamage`, `ZoneTargetResolver`,
`BodyParts`); Help/Harm intent; stamina drain APIs (combat costs deferred);
`HumanoidCombatController` + stance packs / swing triggers under [entities](entities.md)
([player-body-animation](../2026-07_player-body-animation.md)). New combat owns primary-attack
click and calls `RequestAttack` as feedback — do not revive the anim-only LMB intercept.

## Start here (purge targets)

- `Assets/Scripts/SS3D/Systems/Combat/` — entire folder (delete in Phase 0)
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeHitInteraction.cs`
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeWeaponItemExtension.cs` — also strip from Crowbar / Hatchet / KitchenKnife prefabs
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/HandHit.cs`
- `Assets/Scripts/SS3D/Systems/Interactions/InteractionController.cs` — melee-stance LMB → `TryHandlePrimaryAttack` early-return (remove; Phase 1 re-owns click)
- `Assets/Scripts/SS3D/Interactions/IntentController.cs` — orphaned uGUI; Main HUD owns intent
- **Keep:** `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/HumanoidCombatController.cs`

## Extension points

None — system is condemned. Prefer delete over new features. After Phase 0–1, this map
promotes to `partial` via `update-system-docs`.

## Pitfalls

- **Stance LMB ≠ damage:** In `HumanoidCombatMode.Melee`, `InteractionController` consumed
  primary click for anim preview only. Extending that path re-creates the split Phase 0
  exists to erase.
- **Do not hand-edit `Human.prefab`** for combat rewiring — Editor/`PrefabUtility` or wait for
  owning composition pass ([agent-first composition](../2026-07_agent-first-composition.md)).

## Depends on / Used by

- **Depends on (keep-list):** [health](health.md), [interactions-framework](interactions-framework.md), [entities](entities.md)
- **Used by:** none intended until rewrite; Harm-intent discovery currently touches condemned types

## Related docs

- Design (read-only): [Documents/design/combat.md](../../design/combat.md) — intended direction; current Combat code is not the target to extend
- Plan: [combat_implementation_plan.md](../../plans/combat_implementation_plan.md)
- Effort (stance foundation — keep): [2026-07_player-body-animation.md](../2026-07_player-body-animation.md)
- [entities](entities.md), [health](health.md), [stamina](stamina.md)
- [INDEX.md](../INDEX.md)
