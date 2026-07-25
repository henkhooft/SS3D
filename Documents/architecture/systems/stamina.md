> Code paths: Assets/Scripts/SS3D/Systems/Stamina/
> Entry points: StaminaController, StaminaFactory
> Status: partial
> Verified: b1fcfbbce — 2026-07-23

# Stamina

## Overview

Phase **7a-core** rewrite per [stamina.md](../../design/stamina.md) and health plan Phase 7a. Fast exertion pool with health-modulated regen (heart/lungs/blood), carried-weight encumbrance from [inventory](inventory.md) `HumanInventory.CarriedWeight`, sprint drain via `HumanoidController.OnSpeedChangeEvent`, and push-past-empty → `HumanHealthController.ApplyOxyDebt`. **No permanent stamina bar** — obsolete `StaminaBarView` / PlayerCanvas bar purged.

Actions are **not** hard-locked at zero (`CanCommenceInteraction` / `CanContinueInteraction` always true). Exhaustion applies `ExertionPenalty` (0..1) to movement in `HumanoidLivingController` / `HumanoidPredictedMovement`, and — per combat plan Phase 4 — to combat performance: `RangedWeaponProfile.ExhaustionSpreadDegrees` widens the ranged accuracy cone and `MeleeHitInteraction.ComputeExertionTimeMultiplier` (up to 1.6x) lengthens melee windup/recovery. Ranged fire and melee swings both drain via `StaminaController.ServerDepleteStamina` (`RangedWeaponProfile.StaminaCost` / `MeleeWeaponProfile.StaminaCost`). Block stamina drain and dedicated winded screen FX are still deferred (block itself has no interaction yet — combat plan Phase 6, optional for MVP).

## Start here

- `Assets/Scripts/SS3D/Systems/Stamina/StaminaController.cs` — networking, modifiers, overdraw → oxy
- `Assets/Scripts/SS3D/Systems/Stamina/Stamina.cs` / `IStamina.cs` — pool math
- `Assets/Scripts/SS3D/Systems/Stamina/StaminaFactory.cs` — defaults
- `Assets/Scripts/SS3D/Systems/Health/HumanHealthController.cs` — `ApplyOxyDebt` / `ApplyOxyRelief`

## Extension points

- Combat drains: melee swings and ranged fire both call `ServerDepleteStamina` via their profile's `StaminaCost` ([combat](combat.md)); block drain still deferred (no block interaction exists yet).
- Compact HUD indicator near vitals: Main HUD / Phase 6 — do not revive `StaminaBarView`.
- Personal breathing cue: [audio](audio.md) via `StaminaPersonalAudioMapper` (`PersonalAudioSubSystem`), driven from `StaminaController.SyncCurrentStamina`'s `IsOwner` gate — audio.md §4 / stamina.md §4's "heavier breathing" mention.

## Pitfalls

- **Do not reintroduce a permanent stamina bar** on PlayerCanvas — design forbids permanent chrome; oxy/exhaustion feedback rides health screen-effects + future vitals.
- **Modifier refresh is server-tick:** `CarriedWeight` and organ function are read each server update — clients see synced `CurrentStamina` / `ExertionPenalty` SyncVars.

## Depends on / Used by

- **Depends on:** [health](health.md), [inventory](inventory.md), [entities](entities.md), [audio](audio.md) (`PersonalAudioSubSystem`)
- **Used by:** [interactions-runtime](interactions-runtime.md) (`Hand` gates — currently always allow), [combat](combat.md) (swing/fire drains + exertion feedback into windup/cone), movement controllers, Main HUD ranged reticle bloom (`MainHudSubSystem.GetSelectedRangedBloom01`)

## Related docs

- Design (read-only): [stamina.md](../../design/stamina.md), [inventory-storage.md](../../design/inventory-storage.md) §10, [armor.md](../../design/armor.md) §4
- [health_implementation_plan.md](../../plans/health_implementation_plan.md) Phase 7a
- [INDEX.md](../INDEX.md)
