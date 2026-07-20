> Code paths: Assets/Scripts/SS3D/Systems/Combat/, Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/
> Entry points: MeleeHitInteraction, MeleeWeaponItemExtension, HandHit; combat stance via HumanoidCombatController
> Status: partial (Phase 4 melee vertical slice; stance/aim/limp/mirror presentation shipped; blocking/ranged deferred)
> Verified: b00382c9c — 2026-07-20

# Combat

## Overview

Phase 4 ships zone-targeted melee hits wired to the health rewrite. Harm-intent targeted hits resolve body zones via raycast on the `BodyParts` layer, apply brute/burn through `HumanHealthController.ApplyDamage`, and enforce per-weapon windup/recovery. Fists and crowbar are the first weapon profiles; full combat model (blocking, ranged accuracy) remains per design spec.

**Shipped adjacent foundation (presentation):** Peaceful/Melee/Ranged stance locomotion, Injured limp gait (severity idle + oneshots), left-hand Upper Body mirror, aim look-at IK, and melee `AttackSwing` + `AttackVariant` (0–2) live under [entities](entities.md) — see [player-body-animation](../2026-07_player-body-animation.md) and [animation-polish](../2026-07_animation-polish.md). Wiring swing telegraph to windup timing remains a combat build-out task (tune Attack Swing exit/speed in the Animator — do not hardcode clip length in C#).

## Start here

- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeHitInteraction.cs` — Harm intent, windup, zone damage, recovery
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeWeaponItemExtension.cs` — held-item melee (crowbar)
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/HandHit.cs` — empty-hand fists
- `Assets/Scripts/SS3D/Systems/Combat/MeleeWeaponProfile.cs` — damage + timing presets
- `Assets/Scripts/SS3D/Systems/Combat/MeleeRecoveryTracker.cs` — blocks follow-up swings on the hand
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/HumanoidCombatController.cs` — stance / swing triggers (animation side)
- `Assets/Content/WorldObjects/Items/Functional/Tools/Engineering/Crowbar.prefab` — crowbar melee profile

## Extension points

- Add weapons by attaching `MeleeWeaponItemExtension` with a `MeleeWeaponProfile` (or extend profiles in code).
- Combat zone resolution lives in `ZoneTargetResolver.TryResolveCombatZone` (health system); groin uses torso vertical banding.
- Reuse the stance packs already on the humanoid animator when building windup/recovery presentation.

## Depends on / Used by

- **Depends on:** [interactions-framework](interactions-framework.md), [health](health.md) (`ApplyDamage`, `ZoneTargetResolver`, `BodyParts` layer), [entities](entities.md) (stance presentation)
- **Used by:** player Harm-intent targeted interactions

## Related docs

- Design (read-only): [Documents/design/combat.md](../../design/combat.md)
- Plan: [health_implementation_plan.md](../../plans/health_implementation_plan.md) Phase 4
- Effort (stance foundation): [2026-07_player-body-animation.md](../2026-07_player-body-animation.md)
- Effort (melee/limp polish): [2026-07_animation-polish.md](../2026-07_animation-polish.md)
- [entities](entities.md)
