> Code paths: Assets/Scripts/SS3D/Systems/Combat/, Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/
> Entry points: MeleeHitInteraction, HandMeleeExtension, MeleeWeaponItemExtension; swing via HumanoidCombatController.RequestAttack / CmdRunMeleeSwing
> Status: partial
> Verified: 44e290cc9 — 2026-07-20

# Combat

## Overview

Phase 0–1 clean-slate melee per [combat_implementation_plan.md](../../plans/combat_implementation_plan.md).
**Harm primary always swings** (windup → connect → recovery + stamina) via `CmdRunMeleeSwing` —
no collider required at click. Zone damage is resolved only at the **connect** frame from the
client-synced **camera** aim ray (`CmdSyncMeleeAim` → `TryGetMeleeAimRay`), excluding self and
falling back to body `AimYaw`/`AimPitch` when no aim was synced. Misses still consume the full
swing. Empty-hand fists, improvised held items, and dedicated tool profiles (crowbar / hatchet /
kitchen knife). Zone reticle on Main HUD — single-composer presentation (`ZoneReticleDriver` →
`ZoneReticleFrame` → `ZoneTargetReticle.Apply`); see [inventory](inventory.md).

**Melee HUD feedback (design 2A):** recovery drives red lock-on recharge; successful connect plays
white cross flash (whiffs silent). Owner clients mirror recovery via `ServerNotifyMeleeRecovery`.

**Intent ↔ stance:** `C` (and HUD intent chip) toggles Help/Harm. Harm always enters combat stance
(Melee/Ranged from inventory); Help returns Peaceful — see [entities](entities.md).
**Harm is combat-exclusive:** unrestricted world verbs (Drop, Open, MI) are Help-default;
Harm primary never falls through to them when a swing cannot start — see [interactions-runtime](interactions-runtime.md).

Deferred: disarm/grab, ranged, armor, blocking. Stamina swing costs are wired (`MeleeWeaponProfile.StaminaCost`); broader combat stamina (block/fire) still deferred.

**Shipped adjacent foundation (presentation):** Peaceful/Melee/Ranged stance locomotion, Injured limp gait (severity idle + oneshots), left-hand Upper Body mirror, aim look-at IK, and melee `AttackSwing` + `AttackVariant` (0–2) live under [entities](entities.md) — see [player-body-animation](../2026-07_player-body-animation.md) and [animation-polish](../2026-07_animation-polish.md). Wiring swing telegraph to windup timing remains a combat build-out task (tune Attack Swing exit/speed in the Animator — do not hardcode clip length in C#).

## Start here

- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeHitInteraction.cs` — swing start gates + connect-frame zone damage
- `Assets/Scripts/SS3D/Systems/Interactions/InteractionController.cs` — Harm → `TryRunMeleeSwingPrimary` / `CmdRunMeleeSwing`; intent ↔ combat stance; aim + recovery TargetRpcs
- `Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/HumanoidCombatController.cs` — `C` → `RequestToggleIntent`; `RequestAttack` telegraph
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/HandMeleeExtension.cs` — fists on hand prefabs (radial discovery)
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeWeaponItemExtension.cs` — dedicated tool profiles
- `Assets/Scripts/SS3D/Systems/Combat/MeleeWeaponProfile.cs` — timing, damage, `StaminaCost`
- `Assets/Scripts/SS3D/Systems/Combat/MeleeRecoveryTracker.cs` — post-connect recovery lockout + `ReadyProgress01` for HUD brackets
- `Assets/Scripts/SS3D/Systems/Combat/MeleeConnectFeedback.cs` — client signal → reticle cross flash on land
- `Assets/Scripts/SS3D/Systems/Combat/MeleeRecoveryFeedback.cs` — client signal when recovery starts (hit or miss)
- `Assets/Scripts/SS3D/Systems/Health/MeleeDamagePacket.cs` — damage DTO owned by Health
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
2. `C` / HUD chip toggles Help/Harm and combat stance together; Harm shows Melee/Ranged locomotion; HUD intent highlight must update for both paths.
3. Harm LMB with nothing under the reticle — full swing + recovery; no self-damage; brackets recharge red.
4. Harm LMB aimed at limbs through windup — damage applies at connect from camera aim; white cross flash on land (whiff = no flash).
5. Help must not swing. Harm must not Drop / open MI (including while recovering). Optional health debug `H`.

## Pitfalls

- **Do not revive anim-only LMB intercept** in melee stance — telegraph is feedback on swing dispatch only.
- **Do not require a hover collider to start a swing** — Harm primary uses `CmdRunMeleeSwing`; connect resolves hit from synced camera ray.
- **`C` toggles intent, not stance alone** — stance follows Harm via `InteractionController.ApplyCombatModeForIntent`. Hardcoded `cKey` in `HumanoidCombatController`; Input System still binds **Cancel Interaction** to `C` too — see [interactions-runtime](interactions-runtime.md).
- **Connect aim is not stance SyncVars** — owner syncs the **camera mouse ray** via `CmdSyncMeleeAim`; connect must use that ray, not hand→aim.
- **Exclude self on connect/reticle** — pass attacker `HumanHealthController` into `TryResolveHoverZone` or swings hit your own arms.
- **Cancel-on-move is off for melee** — `DelayedInteraction` cancels windup when the root moves; that skipped `StartDelayed` / recovery so walking felt like no cooldown. `MeleeHitInteraction.CancelOnMove` is false (CPR/craft still cancel). Entity-root override remains if cancel is re-enabled.
- **Melee reach uses closest point on zone collider** — ray hit on forearm/hand can be past `RangeLimit`; use `IsMeleeZoneReachInRange`.
- **Limb meshes use AnatomyNode colliders** — include them when armature triggers miss while animating.
- **Client recovery must be TargetRpc'd** — server `MeleeRecoveryTracker` alone leaves pure clients without `IsBusy` / bracket recharge; use `ServerNotifyMeleeRecovery` with the full windup+recovery cycle from swing **Start** (not connect).
- **Swing lock starts at Start** — do not wait until connect to lock; rapid clicks used to cancel in-flight windup via `SupportsMultipleInteractions` and never pay recovery.
- **No LoadingBar on melee** — `MeleeHitInteraction.CreateClient` returns null and Harm primary skips `InteractionOptimisticFeedback`; windup is telegraph, cooldown is reticle lock-on recharge.
- **Reticle presentation is single-composer** — do not reintroduce parallel SetAim/SetLock/Tick writers; color priority and flash live in `ZoneReticleDriver` ([inventory](inventory.md)).
- **Harm is combat-exclusive** — do not reintroduce primary fall-through to Drop/Open when recovery blocks a swing; unrestricted verbs are Help-default in `MatchesIntent`.
- **UNT0026:** use `TryGetComponent` for optional combat components (recovery tracker, weapon extension presence).
- **Prefab wiring:** prefer `MeleePrefabSetup` / PrefabUtility over raw YAML or growing `Human.prefab`.
- **Combat dummy is not on Human.prefab** — `CombatDummyBootstrap` is AddComponent'd only on spawn instances.
- **`spawndummy` needs Administrator** — same bar as `hurt`.
- **Head/torso must not be world containers:** `ContainerInteractive` must not be present on `HumanHead`/`HumanTorso` prefabs (clothing/pocket `AttachedContainer` HUD slots stay). Run **SS3D → Inventory → Strip Head/Torso ContainerInteractive** in the Editor and verify in Play Mode — the tool exists as of [2026-07_human-prefab-decomposition.md](../2026-07_human-prefab-decomposition.md) Phase 0 but has not been executed against the prefabs yet. Surgery organ holes are deferred.

## Depends on / Used by

- **Depends on:** [health](health.md) (`ApplyDamage`, `ZoneTargetResolver`), [stamina](stamina.md) (swing `ServerDepleteStamina`), [interactions-framework](interactions-framework.md), [interactions-runtime](interactions-runtime.md), [entities](entities.md) (stance/swing/aim + dummy spawn), [inventory](inventory.md) (hands / items / zone reticle)
- **Used by:** Harm-intent Run Primary

## Related docs

- Design (read-only): [Documents/design/combat.md](../../design/combat.md) — fork diverges: click always swings; connect resolves hit
- Plan: [combat_implementation_plan.md](../../plans/combat_implementation_plan.md)
- Stance foundation: [2026-07_player-body-animation.md](../2026-07_player-body-animation.md)
- Animation polish: [2026-07_animation-polish.md](../2026-07_animation-polish.md)
- [entities](entities.md), [health](health.md), [stamina](stamina.md), [ingame-console](ingame-console.md)
- [INDEX.md](../INDEX.md)
