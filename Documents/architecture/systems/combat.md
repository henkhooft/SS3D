> Code paths: Assets/Scripts/SS3D/Systems/Combat/, Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/, Assets/Scripts/SS3D/Utils/LineOfSight.cs
> Entry points: Harm primary → `TryRunRangedFirePrimary` / `CmdRunRangedFire` (held `RangedWeaponItemExtension`) else `TryRunMeleeSwingPrimary` / `CmdRunMeleeSwing`
> Status: partial
> Verified: b1fcfbbce — 2026-07-23

# Combat

## Overview

Phase 0–1 melee + Phase 3 ranged vertical slice per [combat_implementation_plan.md](../../plans/combat_implementation_plan.md).

**Melee (unchanged):** Harm primary always swings (windup → connect → recovery) via `CmdRunMeleeSwing`. Connect resolves from synced camera aim (exclude self); living zone or structural Turf. Fists / improvised / crowbar·hatchet·knife.

**Ranged (Phase 3):** Holding `RangedWeaponItemExtension` (M4) — Harm LMB **fires** hitscan inside a weapon accuracy cone (base + recoil + movement bloom + range falloff). Server samples cone, checks shared `LineOfSight` (Default layer), then zone damage or `StructuralDamageSource.Ranged`. Mag + fire cooldown + timed reload (E / Use, or empty-mag fire). Reticle bloom from current spread; cross flash on limb/structural land (whiffs silent). No projectile travel, loose ammo, or armor this pass.

**Intent ↔ stance:** Harm → Melee/Ranged from inventory (`RangedWeaponItemExtension` preferred over trait name); Help → Peaceful. Harm never falls through to Drop/Open/MI.

**Combat stamina drains (Phase 4, fire done, block deferred):** Ranged fire drains
`RangedWeaponProfile.StaminaCost` per shot via `StaminaController.ServerDepleteStamina` (melee
swing already drained this way). Exertion also feeds back into performance: `ExertionPenalty`
widens the ranged accuracy cone (`RangedWeaponProfile.ExhaustionSpreadDegrees`, read in
`AccuracyCone.ComputeSpreadDegrees`) and scales melee windup/recovery up to 1.6x
(`MeleeHitInteraction.ComputeExertionTimeMultiplier`) — both the recovery lock and the actual
connect-timing schedule lengthen together. Main HUD reticle bloom reads the same exertion value
so the visual preview matches server-fired spread.

Deferred: disarm/grab, armor, blocking (including block stamina drain), projectile/thrown.

## Start here

- `Assets/Scripts/SS3D/Systems/Interactions/InteractionController.cs` — Harm branch: ranged fire / reload Cmds; melee swing; aim + TargetRpcs
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/RangedWeaponItemExtension.cs` — profile, mag, recoil, cooldown, reload
- `Assets/Scripts/SS3D/Systems/Combat/RangedWeaponProfile.cs` / `AccuracyCone.cs` / `RangedHitscanResolver.cs`
- `Assets/Scripts/SS3D/Utils/LineOfSight.cs` — shared occlusion (Drop, LocalSpeech, combat)
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeHitInteraction.cs` — melee swing + connect
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeWeaponItemExtension.cs` / `HandMeleeExtension.cs`
- `Assets/Scripts/SS3D/Systems/Combat/MeleeWeaponProfile.cs` / `MeleeStructuralHitResolver.cs` / `MeleeRecoveryTracker.cs`
- `Assets/Scripts/SS3D/Systems/Combat/Editor/RangedPrefabSetup.cs` — **SS3D → Combat → Setup Ranged Prefabs (M4)**
- `Assets/Scripts/SS3D/Systems/Combat/Editor/MeleePrefabSetup.cs` — melee hands/tools
- `Assets/Scripts/SS3D/Systems/Combat/CombatDummyBootstrap.cs` + `spawndummy`

## Extension points

- New firearm: add `RangedWeaponItemExtension` + profile via `RangedPrefabSetup` / PrefabUtility — do not hand-edit `Human.prefab`.
- Stance: `HumanoidBodyStateBridge.ResolveCombatStance` prefers the extension component.
- Shared LOS: call `LineOfSight.HasLineOfSight` / `TryGetFirstHit` — do not fork parallel raycasts.

## Testing

1. Host admin: `spawndummy`; Harm + empty hand — melee as before.
2. Spawn/give M4; Harm — Ranged stance; LMB fires; zone damage at range; reticle blooms when moving/recoiling.
3. Wall between you and dummy — shot blocked (no limb damage); wall may take structural force.
4. Empty mag or **E** — timed reload, then fire again. Help does not fire.
5. Help + M4 must not swing/fire; Harm must not Drop.
6. Fire/swing repeatedly until stamina is low — ranged spread should visibly widen and melee windup/recovery should visibly slow versus a fresh attack at full stamina.

## Pitfalls

- **Held gun never melee-swings** — `TryRunRangedFirePrimary` returns true whenever a ranged extension is held (including empty/cooldown); do not fall through to `CmdRunMeleeSwing`.
- **Host optimistic fire lock** — same as melee: only optimistic-cooldown on pure clients (`!IsServer`); host uses server consume + TargetRpc.
- **Zone ray default is 8 m** — ranged passes `profile.MaxRangeMeters` into `TryResolveHoverZone`; do not hardcode melee default for hitscan.
- **Reload via E bypasses intent** — `ReloadRangedInteraction` is Help-default in discovery; Harm reload uses `CmdRunRangedReload` from Use / empty fire.
- **Reticle bloom is single-composer** — set via `ZoneReticleDriver.SetBloomInput` only; no parallel writers.
- **Melee windup lengthening has two call sites that must stay in sync** — `MeleeHitInteraction.ServerBeginSwing` returns the exertion-scaled windup seconds; `InteractionController.CmdRunMeleeSwing` must pass that return value (not raw `profile.WindupSeconds`) into `ServerScheduleMeleeConnect`, or the recovery-lock UI and the actual connect timer drift apart under exhaustion.
- Melee pitfalls (connect aim, exclude self, structural reach, Harm whitelist, etc.) still apply — see git history / prior map notes.

## Depends on / Used by

- **Depends on:** [health](health.md), [stamina](stamina.md) (melee + ranged fire costs, exertion feedback into windup/cone), [interactions-runtime](interactions-runtime.md), [entities](entities.md), [inventory](inventory.md), [structural-destruction](structural-destruction.md)
- **Used by:** Harm-intent Run Primary; Hotkeys Use (reload)

## Related docs

- Design: [Documents/design/combat.md](../../design/combat.md)
- Plan: [combat_implementation_plan.md](../../plans/combat_implementation_plan.md) (Phase 3 shipped)
- [entities](entities.md), [health](health.md), [INDEX.md](../INDEX.md)
