> Code paths: Assets/Scripts/SS3D/Systems/Combat/, Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/
> Entry points: MeleeHitInteraction, HandMeleeExtension, MeleeWeaponItemExtension; swing via HumanoidCombatController.RequestAttack
> Status: partial
> Verified: 39fe3fbb2 — 2026-07-19

# Combat

## Overview

Phase 0–1 clean-slate melee per [combat_implementation_plan.md](../../plans/combat_implementation_plan.md).
Harm-intent Hit resolves zones via `ZoneTargetResolver`, applies `MeleeDamagePacket` through
`HumanHealthController.ApplyDamage`, and enforces windup/recovery. Empty-hand fists
(`HandMeleeExtension`), improvised any-held-item (`Item.CreateSourceInteractions`), and dedicated
profiles on crowbar / hatchet / kitchen knife. Run Primary plays swing telegraph via
`HumanoidCombatController.RequestAttack` then dispatches the delayed Hit — presentation does not
consume LMB alone. Zone-label reticle chip (main-hud §6) lives on Main HUD — see [inventory](inventory.md).

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
- `Assets/Scripts/SS3D/Systems/Combat/CombatDummyBootstrap.cs` — freezes controls on mindless test Human
- `Assets/Scripts/SS3D/Systems/Entities/EntitySubSystem.cs` — `ServerSpawnCombatDummy`
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/SpawnDummyCommand.cs` — console `spawndummy`
- `Assets/Scripts/SS3D/Systems/Combat/Editor/MeleePrefabSetup.cs` — menu **SS3D → Combat → Setup Melee Prefabs**

## Extension points

- Dedicated weapons: add `MeleeWeaponItemExtension` with a profile (Editor menu or PrefabUtility).
- Improvised fallback is automatic on `Item` when no `MeleeWeaponItemExtension` is present.
- Empty-hand fists: `HandMeleeExtension` on `HumanHandLeft` / `HumanHandRight` prefabs — do not hand-edit `Human.prefab`.

## Testing

1. Host Play Mode as admin, console: `spawndummy` — mindless Human ~2m ahead (controls frozen).
2. Harm intent, LMB limbs — windup + swing + zone damage. Help must not Hit.
3. Hover limbs — Main HUD reticle shows zone chip (`chest`, `l_arm`, …); optional health debug `H`.

## Pitfalls

- **Do not revive anim-only LMB intercept** in melee stance — that split was Phase 0 purged. Telegraph is feedback on Hit dispatch only.
- **UNT0026:** use `TryGetComponent` for optional combat components (recovery tracker, weapon extension presence).
- **Prefab wiring:** prefer `MeleePrefabSetup` / PrefabUtility over raw YAML or growing `Human.prefab`.
- **Combat dummy is not on Human.prefab** — `CombatDummyBootstrap` is AddComponent'd only on spawn instances.
- **Hit fails with target index -2 / no damage despite swing bar:** client discovers on a body-part `Selectable` (e.g. `HumanTorso`); server revalidates on the Entity `NetworkObject` root — wire indices diverge. `InteractionController.TryResolveDispatchedInteraction` falls back to generic name. Melee is **raycast zone damage after windup**, not limb physics contact — broken swing anim does not block a resolved Hit.
- **`spawndummy` needs Administrator** — same bar as `hurt`.
- **Head/torso must not be world containers:** `ContainerInteractive` stripped from `HumanHead`/`HumanTorso` prefabs (clothing/pocket `AttachedContainer` HUD slots kept). Re-run **SS3D → Inventory → Strip Head/Torso ContainerInteractive** if it returns. Surgery organ holes are deferred.

## Depends on / Used by

- **Depends on:** [health](health.md) (`ApplyDamage`, `ZoneTargetResolver`), [interactions-framework](interactions-framework.md), [entities](entities.md) (stance/swing + dummy spawn), [inventory](inventory.md) (hands / items / zone reticle)
- **Used by:** Harm-intent Run Primary / radial Hit

## Related docs

- Design (read-only): [Documents/design/combat.md](../../design/combat.md)
- Plan: [combat_implementation_plan.md](../../plans/combat_implementation_plan.md)
- Stance foundation: [2026-07_player-body-animation.md](../2026-07_player-body-animation.md)
- [entities](entities.md), [health](health.md), [stamina](stamina.md), [ingame-console](ingame-console.md)
- [INDEX.md](../INDEX.md)
