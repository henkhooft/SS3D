> Code paths: Assets/Scripts/SS3D/Systems/Combat/, Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/
> Entry points: MeleeHitInteraction, HandMeleeExtension, MeleeWeaponItemExtension; swing via HumanoidCombatController.RequestAttack
> Status: partial
> Verified: 0e7e98d5c — 2026-07-19

# Combat

## Overview

Phase 0–1 clean-slate melee per [combat_implementation_plan.md](../../plans/combat_implementation_plan.md).
Harm-intent Hit resolves zones via `ZoneTargetResolver`, applies `MeleeDamagePacket` through
`HumanHealthController.ApplyDamage`, and enforces windup/recovery. Empty-hand fists
(`HandMeleeExtension`), improvised any-held-item (`Item.CreateSourceInteractions`), and dedicated
profiles on crowbar / hatchet / kitchen knife. Run Primary plays swing telegraph via
`HumanoidCombatController.RequestAttack` then dispatches the delayed Hit — presentation does not
consume LMB alone.

Deferred: disarm/grab, ranged, combat stamina drains, armor, blocking.

## Start here

- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeHitInteraction.cs` — Harm Hit, windup, zone damage, recovery
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/HandMeleeExtension.cs` — fists on hand prefabs
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeWeaponItemExtension.cs` — dedicated tool profiles
- `Assets/Scripts/SS3D/Systems/Combat/MeleeWeaponProfile.cs` — Fists / Improvised / Crowbar / Hatchet / KitchenKnife
- `Assets/Scripts/SS3D/Systems/Combat/MeleeRecoveryTracker.cs` — post-hit recovery lockout on Hand
- `Assets/Scripts/SS3D/Systems/Health/MeleeDamagePacket.cs` — damage DTO owned by Health
- `Assets/Scripts/SS3D/Systems/Interactions/InteractionController.cs` — `TryPlayMeleeSwingTelegraph` on Run Primary
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/HumanoidCombatController.cs` — stance toggle + `RequestAttack`
- `Assets/Scripts/SS3D/Systems/Combat/Editor/MeleePrefabSetup.cs` — menu **SS3D → Combat → Setup Melee Prefabs**

## Extension points

- Dedicated weapons: add `MeleeWeaponItemExtension` with a profile (Editor menu or PrefabUtility).
- Improvised fallback is automatic on `Item` when no `MeleeWeaponItemExtension` is present.
- Empty-hand fists: `HandMeleeExtension` on `HumanHandLeft` / `HumanHandRight` prefabs — do not hand-edit `Human.prefab`.

## Pitfalls

- **Do not revive anim-only LMB intercept** in melee stance — that split was Phase 0 purged. Telegraph is feedback on Hit dispatch only.
- **UNT0026:** use `TryGetComponent` for optional combat components (recovery tracker, weapon extension presence).
- **Prefab wiring:** prefer `MeleePrefabSetup` / PrefabUtility over raw YAML or growing `Human.prefab`.

## Depends on / Used by

- **Depends on:** [health](health.md) (`ApplyDamage`, `ZoneTargetResolver`), [interactions-framework](interactions-framework.md), [entities](entities.md) (stance/swing), [inventory](inventory.md) (hands / items)
- **Used by:** Harm-intent Run Primary / radial Hit

## Related docs

- Design (read-only): [Documents/design/combat.md](../../design/combat.md)
- Plan: [combat_implementation_plan.md](../../plans/combat_implementation_plan.md)
- Stance foundation: [2026-07_player-body-animation.md](../2026-07_player-body-animation.md)
- [entities](entities.md), [health](health.md), [stamina](stamina.md)
- [INDEX.md](../INDEX.md)
