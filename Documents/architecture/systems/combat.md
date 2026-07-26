> Code paths: Assets/Scripts/SS3D/Systems/Combat/, Assets/Scripts/SS3D/Systems/Entities/Humanoid/Body/, Assets/Scripts/SS3D/Utils/LineOfSight.cs
> Entry points: Harm primary → `TryRunRangedFirePrimary` / `CmdRunRangedFire` (held `RangedWeaponItemExtension`) else `TryRunMeleeSwingPrimary` / `CmdRunMeleeSwing`
> Status: partial
> Verified: e1d43a531 — 2026-07-26

# Combat

## Overview

Phase 0–1 melee + Phase 3 ranged + Phase 5 armor slice per [combat_implementation_plan.md](../../plans/combat_implementation_plan.md).

**Melee (unchanged):** Harm primary always swings (windup → connect → recovery) via `CmdRunMeleeSwing`. Connect resolves from synced camera aim (exclude self); living zone or structural Turf. Fists / improvised / crowbar·hatchet·knife.

**Ranged (Phase 3 + feel Phase 1–6):** Holding `RangedWeaponItemExtension` (M4) — Harm LMB **fires** hitscan inside a weapon accuracy cone (base + recoil + movement bloom + range falloff). Server samples cone, resolves a living zone first, then checks shared `LineOfSight` (Default + Walls) only to that limb; otherwise structural turf / soft surface. Mag + fire cooldown + timed reload (E / Use, or empty-mag fire). Reticle bloom tracks current spread at aim distance; damaging hits flash the cross. **Diegetic impact:** muzzle flash + bullet holes; gold/grey debug spheres are **off by default** (`rangeddebug on|off`). **Muzzle flash:** procedural point light + particle burst at the weapon `Muzzle` socket (`MuzzleFlashVfx`), broadcast via `ObserversNotifyMuzzleFlash` so all observers see it. **Bullet holes:** non-living impacts spawn a capped URP `DecalProjector` (`BulletHoleDecalSpawner`, textures under `Assets/Art/Textures/World/VFX/BulletHoles/`) via `ObserversNotifyBulletHole`; living hits skip holes. **Fire/reload anim:** `RequestAttack(FireRifle|Reload)` → Base `Mix_FiringRifle` / `Mix_Reloading` (Ranged stance). **Two-hand M4:** `RequiresBothHands` + `RequiredHand=Right` — may only occupy the right hand; left is reserved while wielded (`TwoHandedWeaponRules` via hand `CanContainItem`). **Pickup UX:** active left + empty right still offers Pick up / HUD drop — auto-routes into right and selects that hand; left well shows a reserved ban badge while the rifle is held, and hand-swap / HUD select cannot move onto that reserved hand. Fire/reload/stance/bloom resolve from the right-held rifle; `MirrorUpperBody` stays false. No projectile travel or loose ammo this pass.

**Armor (Phase 5):** `HumanHealthController.ApplyDamage(BodyZone, float, float)` — the single chokepoint both melee and ranged funnel through — runs incoming brute/burn through every worn armor piece covering the hit zone before it reaches the limb model. Worn pieces are discovered from existing clothing containers (`ContainerType.IsWornSlot()`), no new equip UI. `ArmorItemExtension` (per-item, `SyncVar` integrity) depletes by the amount actually absorbed; a depleted piece stops absorbing. Pure math in `ArmorSimulation.ResolveAbsorption`. Environmental seal/breach (`armor.md` §3) deferred — no environment→health exposure pipeline exists yet.

**Intent ↔ stance:** Harm → Melee/Ranged from inventory (`RangedWeaponItemExtension` preferred over trait name); Help → Peaceful. Harm never falls through to Drop/Open/MI.

**Combat stamina drains (Phase 4, fire done, block deferred):** Ranged fire drains
`RangedWeaponProfile.StaminaCost` per shot via `StaminaController.ServerDepleteStamina` (melee
swing already drained this way). Exertion also feeds back into performance: `ExertionPenalty`
widens the ranged accuracy cone (`RangedWeaponProfile.ExhaustionSpreadDegrees`, read in
`AccuracyCone.ComputeSpreadDegrees`) and scales melee windup/recovery up to 1.6x
(`MeleeHitInteraction.ComputeExertionTimeMultiplier`) — both the recovery lock and the actual
connect-timing schedule lengthen together. Main HUD reticle bloom reads the same exertion value
so the visual preview matches server-fired spread.

Deferred: disarm/grab, environmental seal/breach, armor wear visuals, blocking (including block
stamina drain), projectile/thrown.

## Start here

- `Assets/Scripts/SS3D/Systems/Interactions/InteractionController.cs` — Harm branch: ranged fire / reload Cmds; melee swing; aim + TargetRpcs
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/RangedWeaponItemExtension.cs` — profile, mag, recoil, cooldown, reload
- `Assets/Scripts/SS3D/Systems/Combat/RangedWeaponProfile.cs` / `AccuracyCone.cs` / `RangedHitscanResolver.cs` / `RangedShotFeedback.cs` / `MuzzleFlashVfx.cs` / `TwoHandedWeaponRules.cs` / `BulletHoleDecalSpawner.cs` / `BulletHoleVfxCatalog.cs`
- `Assets/Scripts/SS3D/Systems/Combat/CombatAudioTrackIds.cs` — `AssetDatabases.Sounds` clip ids (SS14 rifle fire/empty/mag/cock + surface ricochet set) registered under `Assets/Art/Sound/Items/Weapons/Firearms/SS14/`; `InteractionController` plays fire/empty/surface via `AudioSubSystem.PlayAudioSource`, reload-complete mag-in/cock from `RangedWeaponItemExtension.TryCompleteReloadIfDue` (Sfx — gains occlusion via [audio](audio.md) `AudioSourceOcclusion` for free)
- `Assets/Scripts/SS3D/Utils/LineOfSight.cs` — shared occlusion (Drop, LocalSpeech, combat)
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeHitInteraction.cs` — melee swing + connect
- `Assets/Scripts/SS3D/Systems/Combat/Interactions/MeleeWeaponItemExtension.cs` / `HandMeleeExtension.cs`
- `Assets/Scripts/SS3D/Systems/Combat/MeleeWeaponProfile.cs` / `MeleeStructuralHitResolver.cs` / `MeleeRecoveryTracker.cs`
- `Assets/Scripts/SS3D/Systems/Combat/Editor/CombatContentPrefabRecipes.cs` — **SS3D → Combat → Run Content Prefab Recipes**
- `Assets/Scripts/SS3D/Systems/Combat/Editor/RangedPrefabSetup.cs` — ranged M4 statics (tier B)
- `Assets/Scripts/SS3D/Systems/Combat/Editor/MeleePrefabSetup.cs` — melee hands/tools statics (tier B)
- `Assets/Scripts/SS3D/Systems/Combat/CombatDummyBootstrap.cs` + `spawndummy` — freezes controls; equips `JumpsuitSecurity` for armor tests
- `Assets/Scripts/SS3D/Systems/Combat/ArmorProfile.cs` / `ArmorSimulation.cs` / `ArmorItemExtension.cs` — per-zone absorption data, math, per-item integrity
- `Assets/Scripts/SS3D/Systems/Combat/Editor/ArmorPrefabSetup.cs` — armor statics (interim: `JumpsuitSecurity.prefab`; world form via inventory clothing presentation on Grey base)
- `Assets/Scripts/SS3D/Systems/Health/HumanHealthController.cs` — `ApplyArmorAbsorption` (armor hook inside `ApplyDamage`)

## Extension points

- New firearm: add `RangedWeaponItemExtension` + profile via `RangedPrefabSetup` / **SS3D → Combat → Run Content Prefab Recipes** — do not hand-edit `Human.prefab`.
- New armor piece: add `ArmorItemExtension` + profile via `ArmorPrefabSetup` / same aggregator on a clothing item prefab — do not hand-edit `Human.prefab`; coverage is `BodyZoneMask`, independent of which clothing slot the item occupies.
- Stance: `HumanoidBodyStateBridge.ResolveCombatStance` prefers the extension component.
- Shared LOS: call `LineOfSight.HasLineOfSight` / `TryGetFirstHit` — do not fork parallel raycasts.

## Testing

1. Host admin: `spawndummy`; Harm + empty hand — melee as before.
2. Spawn/give M4 (left active is fine if right is empty — auto-routes to right and selects it); Harm — Ranged stance; LMB fires; zone damage at range; reticle blooms with movement/recoil; impact marker shows where the round lands; muzzle flash pops at the barrel tip (visible to other clients too); wall/floor impacts leave bullet-hole decals. Left hand well shows a reserved badge and cannot be selected or receive items while the M4 is held.
3. Wall between you and dummy — shot blocked (no limb damage); wall may take structural force.
4. Empty mag or **E** — timed reload, then fire again. Help does not fire.
5. Help + M4 must not swing/fire; Harm must not Drop.
6. `spawndummy` equips `JumpsuitSecurity` automatically; Harm-melee/ranged covered zones
   (chest/limbs) take reduced brute, head (uncovered) takes full damage; enough hits deplete
   integrity and damage reverts to unmitigated. Unequip/drop keeps the same Item (armor SyncVar)
   and restores folded world form. Equipment-doll HUD icons use the worn-shaped mesh; hands show folded.
7. Fire/swing repeatedly until stamina is low — ranged spread should visibly widen and melee windup/recovery should visibly slow versus a fresh attack at full stamina.

## Pitfalls

- **Held gun never melee-swings** — `TryRunRangedFirePrimary` returns true whenever a ranged extension is held (including empty/cooldown); do not fall through to `CmdRunMeleeSwing`.
- **Host optimistic fire lock** — same as melee: only optimistic-cooldown on pure clients (`!IsServer`); host uses server consume + TargetRpc.
- **Zone ray default is 8 m** — ranged passes `profile.MaxRangeMeters` into `TryResolveHoverZone`; do not hardcode melee default for hitscan.
- **Reload via E bypasses intent** — `ReloadRangedInteraction` is Help-default in discovery; Harm reload uses `CmdRunRangedReload` from Use / empty fire.
- **Reticle bloom is single-composer** — set via `ZoneReticleDriver.SetBloomInput` only; no parallel writers. Bloom uses live aim-ray distance (not a fake mid-range), so close targets stay tight.
- **Melee windup lengthening has two call sites that must stay in sync** — `MeleeHitInteraction.ServerBeginSwing` returns the exertion-scaled windup seconds; `InteractionController.CmdRunMeleeSwing` must pass that return value (not raw `profile.WindupSeconds`) into `ServerScheduleMeleeConnect`, or the recovery-lock UI and the actual connect timer drift apart under exhaustion.
- **Ranged impact marker is debug-only** — `RangedShotFeedback` after `TargetNotifyRangedFireState`; gold = damaging connect (also cross-flash), grey = surface whiff. Spheres default **off**; `rangeddebug on|off|status` (client). Living hits pull the marker toward the shooter so it isn't buried inside BodyParts colliders. Normal play uses bullet holes + muzzle flash — do not re-enable spheres as diegetic feedback.
- **Muzzle flash is ObserversRpc** — `ObserversNotifyMuzzleFlash` → each client parents `MuzzleFlashVfx` to the local held `Muzzle` socket (light + particles at that transform); server-sampled world pose is fallback only. Do not spawn the flash only from a baked server world point or remotes/owner visuals can drift.
- **Bullet holes are ObserversRpc + world-floor decal layers** — `ObserversNotifyBulletHole` → `BulletHoleDecalSpawner` (cap 96). Living hits skip holes. Keep URP Decal material **Opaque**; projectors use `DecalRenderingLayers.WorldFloorProjectorMask` and `BloodDecalSpawner.RotationOntoSurface` (same floor-paint pitfalls as health blood). Catalog uses **Shreds only** (not `BulletHole.png`); white masks are charcoal-tinted at runtime via `Hidden/SS3D/MultiplyTint` like blood. Structural hits pass the wall collider normal (not a second probe).
- **Holes miss walls / look half-clipped** — not a Lit-vs-toon issue (ST floors receive fine). `RotationOntoSurface` must pass world **up** into `LookRotation` for vertical faces — a `Cross` "tangent" as the up hint rolls the projector 90° so the volume only grazes the wall. Near-vertical hits also use a deeper projector box (`WallVolumeDepth`) so thin wall skins still intersect the volume. Blood floor stamps never hit walls: `BloodDecalSpawner` only raycasts **down**.
- **Holes only on URP Lit floors (e.g. TileGrey), missing on ST walls/floors** — Decal Renderer **Use Rendering Layers** must stay on (`decalLayers: 1` on `SS3D_ForwardPlusRenderer`). A lighting retune once flipped it off; with layers on, tiles need `ReceiveWorldDecals` (stamped by `PlacedTileObject`) and ST must sample DBuffer. Do not turn layers off to “fix” visibility.
- **Surface impact SFX** — non-living hits play a random `CombatAudioTrackIds.SurfaceHit` clip (`BulletHit` + `Ric1`–`Ric5`) at the impact point; living hits keep health `FleshHit`. Do not play surface ricochets on limb connects.
- **Hitscan must resolve living before full-range Default occlusion** — Characters are not on the Default mask, so a max-range Default cast goes *through* the dummy and can “block” on floor/props behind them (no limb damage; marker far behind or easy to miss). Order: zone hit → LOS (Default+Walls) only to that limb → structural/soft. Do not early-out on `IsOccluded` for the full weapon range.
- **Armor absorption is a single chokepoint** — lives inside `HumanHealthController.ApplyDamage(BodyZone, float, float)`, not duplicated in melee/ranged call sites; also applies to `StructuralDamageSubSystem`'s debris-collapse call (intentional, not excluded).
- **Armor coverage ≠ clothing slot** — `ArmorProfile.CoveredZones` (`BodyZoneMask`) is independent of which `ContainerType` slot the item occupies; only "is it worn" (`IsWornSlot()`) gates lookup, not slot identity.
- **Armor Item is not a dual prefab** — `ArmorItemExtension` stays on the clothing Item (`JumpsuitSecurity`); world folded look is `ClothingItemPresentation` on the same NO ([inventory](inventory.md)). Do not spawn a separate folded NetworkObject for drops.
- Melee pitfalls (connect aim, exclude self, structural reach, Harm whitelist, etc.) still apply — see git history / prior map notes.
- **M4 gun audio is the SS14 rifle set** — `GunFire` = `Rifle`/`Rifle2`; empty = `Empty`; reload start = `LtRifleMagOut`; reload complete = `LtRifleMagIn` + `LtRifleCock` (server `TryCompleteReloadIfDue`). Legacy SS3D-Art `Gun Firing1-2` / AR-15 folder / Pump Shotgun remain on disk but unwired. Surface impacts: `SurfaceHit` (`BulletHit` + `Ric1`–`Ric5`).
- **Two-hand M4 is right-only** — `RangedWeaponProfile.RequiresBothHands` + `RequiredHand=Right`. Equip/transfer gates live in `AttachedContainer.CanContainItem` via `TwoHandedWeaponRules` (also Pickup/TakeFirst). Do not only gate `PickupInteraction` — HUD drag must fail too. **Auto-route:** `TryResolveHandForItem` / `Hand.Pickup` / HUD drop redirect put the rifle in the required hand when the off-hand is active; `Hands` selects that grip on attach. **Reserved HUD:** `MainHudSubSystem.RefreshHandReservedState` → `inventory-slot--reserved` ban badge on the off-hand well. **No hand-switch onto reserved:** `Hands.CanSelectHand` / `CmdSetActiveHand` / swap / HUD click ignore a reserved off-hand. Fire/reload/bloom/stance use `TryGetWieldedRangedWeapon` (right-held rifle), not bare `SelectedHand`. `MirrorUpperBody` must stay false while the rifle is wielded.

## Depends on / Used by

- **Depends on:** [health](health.md), [stamina](stamina.md) (melee + ranged fire costs, exertion feedback into windup/cone; armor weight also feeds `CarriedWeight`), [interactions-runtime](interactions-runtime.md), [entities](entities.md), [inventory](inventory.md) (worn armor lookup via `HumanInventory`/`ContainerType`), [structural-destruction](structural-destruction.md), [audio](audio.md) (`AudioSubSystem.PlayAudioSource` — gunfire/reload SFX), [ingame-console](ingame-console.md) (`rangeddebug`)
- **Used by:** Harm-intent Run Primary; Hotkeys Use (reload)

## Related docs

- Design: [Documents/design/combat.md](../../design/combat.md), [Documents/design/armor.md](../../design/armor.md)
- Plan: [combat_implementation_plan.md](../../plans/combat_implementation_plan.md) (Phase 3, Phase 5 shipped)
- Clothing world form: [clothing_world_presentation.plan.md](../../plans/clothing_world_presentation.plan.md), [inventory](inventory.md)
- [entities](entities.md), [health](health.md), [inventory](inventory.md), [INDEX.md](../INDEX.md)
