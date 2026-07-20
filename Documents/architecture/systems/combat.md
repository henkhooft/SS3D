> Code paths: Assets/Scripts/SS3D/Systems/Combat/, Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/
> Entry points: MeleeHitInteraction, HandMeleeExtension, MeleeWeaponItemExtension; swing via HumanoidCombatController.RequestAttack / CmdRunMeleeSwing
> Status: partial
> Verified: bd4ec5e61 — 2026-07-20

# Combat

## Overview

Phase 0–1 clean-slate melee per [combat_implementation_plan.md](../../plans/combat_implementation_plan.md).
**Harm primary always swings** (windup → connect → recovery + stamina) via `CmdRunMeleeSwing` —
no collider required at click. Zone damage is resolved only at the **connect** frame from the
client-synced mouse aim point (`CmdSyncMeleeAim` → `TryGetMeleeAimPoint`), falling back to body
`AimYaw`/`AimPitch` when no aim was synced. Misses still consume the full swing. Empty-hand
fists, improvised held items, and dedicated tool profiles (crowbar / hatchet / kitchen knife).
Zone reticle on Main HUD — see [inventory](inventory.md).

Deferred: disarm/grab, ranged, armor, blocking. Stamina swing costs are wired (`MeleeWeaponProfile.StaminaCost`); broader combat stamina (block/fire) still deferred.

**Shipped adjacent foundation (presentation):** Peaceful/Melee/Ranged stance locomotion, Injured limp gait (severity idle + oneshots), left-hand Upper Body mirror, aim look-at IK, and melee `AttackSwing` + `AttackVariant` (0–2) live under [entities](entities.md) — see [player-body-animation](../2026-07_player-body-animation.md) and [animation-polish](../2026-07_animation-polish.md). Wiring swing telegraph to windup timing remains a combat build-out task (tune Attack Swing exit/speed in the Animator — do not hardcode clip length in C#).

## Start here

- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeHitInteraction.cs` — swing start gates + connect-frame zone damage
- `Assets/Scripts/SS3D/Systems/Interactions/InteractionController.cs` — Harm → `TryRunMeleeSwingPrimary` / `CmdRunMeleeSwing`
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/HandMeleeExtension.cs` — fists on hand prefabs (radial discovery)
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeWeaponItemExtension.cs` — dedicated tool profiles
- `Assets/Scripts/SS3D/Systems/Combat/MeleeWeaponProfile.cs` — timing, damage, `StaminaCost`
- `Assets/Scripts/SS3D/Systems/Combat/MeleeRecoveryTracker.cs` — post-connect recovery lockout on Hand
- `Assets/Scripts/SS3D/Systems/Health/MeleeDamagePacket.cs` — damage DTO owned by Health
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
2. Harm intent, LMB with nothing under the reticle — full swing + recovery; no damage.
3. Harm LMB aimed at limbs through windup — damage applies at connect from current aim.
4. Help must not swing. Reticle: grey idle / blue in-range; red pulse only if connect applied damage (whiff = no pulse). Optional health debug `H`.

## Pitfalls

- **Do not revive anim-only LMB intercept** in melee stance — telegraph is feedback on swing dispatch only.
- **Do not require a hover collider to start a swing** — Harm primary uses `CmdRunMeleeSwing`; connect resolves hit from synced mouse aim.
- **Connect aim is not stance SyncVars** — peaceful Harm never updates `AimYaw`/`AimPitch`. Owner syncs camera aim via `CmdSyncMeleeAim` during windup; connect prefers that over body aim.
- **Cancel-on-move uses entity root** — `DelayedInteraction` defaults to the hand transform; melee overrides to `Entity` so swing bone motion does not cancel windup.
- **Connect ray from hand can skew** — building hand→aimPoint after swing anim starts may miss moving arm zones; prefer camera ray matching the reticle (open follow-up).
- **UNT0026:** use `TryGetComponent` for optional combat components (recovery tracker, weapon extension presence).
- **Prefab wiring:** prefer `MeleePrefabSetup` / PrefabUtility over raw YAML or growing `Human.prefab`.
- **Combat dummy is not on Human.prefab** — `CombatDummyBootstrap` is AddComponent'd only on spawn instances.
- **`spawndummy` needs Administrator** — same bar as `hurt`.
- **Head/torso must not be world containers:** `ContainerInteractive` stripped from `HumanHead`/`HumanTorso` prefabs (clothing/pocket `AttachedContainer` HUD slots kept). Re-run **SS3D → Inventory → Strip Head/Torso ContainerInteractive** if it returns. Surgery organ holes are deferred.

## Depends on / Used by

- **Depends on:** [health](health.md) (`ApplyDamage`, `ZoneTargetResolver`), [stamina](stamina.md) (swing `ServerDepleteStamina`), [interactions-framework](interactions-framework.md), [entities](entities.md) (stance/swing/aim + dummy spawn), [inventory](inventory.md) (hands / items / zone reticle)
- **Used by:** Harm-intent Run Primary

## Related docs

- Design (read-only): [Documents/design/combat.md](../../design/combat.md) — fork diverges: click always swings; connect resolves hit
- Plan: [combat_implementation_plan.md](../../plans/combat_implementation_plan.md)
- Stance foundation: [2026-07_player-body-animation.md](../2026-07_player-body-animation.md)
- Animation polish: [2026-07_animation-polish.md](../2026-07_animation-polish.md)
- [entities](entities.md), [health](health.md), [stamina](stamina.md), [ingame-console](ingame-console.md)
- [INDEX.md](../INDEX.md)
