> Code paths: Assets/Scripts/SS3D/Systems/Health/
> Entry points: HumanHealthController, HealthSimulation, OrganSimulation
> Status: partial (Phase 5b severing + turf env→health shipped; vitals HUD Phase 6 remainder; armor seal deferred)
> Verified: 9965980a7 — 2026-07-25

# Health

## Overview

Greenfield rewrite per [health_implementation_plan.md](../../plans/health_implementation_plan.md). Phase 1 shipped bleeding, bandage, and VFX. Phase 2 wires asset-backed organs into pool math, cardiac arrest, and movement debuffs (`Snapshot.MovementSpeedMultiplier` — consumed by humanoid gait/limp presentation after the develop integration). Phase 3 adds multi-threshold critical state, cardiac arrest → defib window, and chest defibrillation. Phase 4 adds BodyParts raycast zone resolution for melee combat. Phase 5 adds field treatments (burn dressing, splint, O2, CPR, transfusion, antitoxin). Phase 5b adds limb severing (zone `IsSevered`, anatomy hide, world drops, head mind-swap). Bleeding visuals now use tuned particle streams plus URP Decal blood marks (body + floor). Bleed drain uses `BleedingBloodDrainScale = 0.010` with oxy gain / arrest brain drain synced so hypoxia tracks bleed (see plan hemorrhage tuning).

Local-owner [screen-effects](screen-effects.md) are driven from `HealthSnapshot` via `HealthScreenEffectMapper` (dying/critical, blood-loss tunnel vision, oxy debt, concussion, unconscious) plus hit flash on `ApplyDamage`, and turf temp/fire via `AtmosScreenEffectMapper` from `HealthSnapshot.Environment`. Main HUD alert icons merge `HealthAlertStackMapper` + `AtmosAlertStackMapper` (Hot/Cold/pressure/Fire + LowOxygen from debt or turf). Server `TickHealthFromEnvironment` samples the occupant tile each 1 Hz tick: normalized O₂ breathability, O₂→CO₂ breath exchange, plasma toxin intake, and hot/cold/fire burn on chest. `atmosdamage off` / Health Debug checkbox skips turf coupling (`HealthEnvironmentSettings.AtmosphericDamageDisabled`). Vitals cluster UITK and examine-self readout remain Phase 6.

**Stamina Phase 7a core shipped:** see [stamina](stamina.md) — push-past-empty calls `ApplyOxyDebt`; obsolete `StaminaBar` purged from PlayerCanvas. Combat stamina costs still deferred.

Phase 0d strips legacy health components from `Human.prefab` and rewires a thinner root — do not dual-stack or grow the mega-prefab ([agent-first composition](../2026-07_agent-first-composition.md), [health_implementation_plan.md](../../plans/health_implementation_plan.md) Phase 0d).

**Body presentation:** Health emits collapse intent only (`BodyPresentationIntent` → `Ragdoll.ServerSetPresentation`). Death is `Human.Kill` → `ServerDeathRagdoll`. Do not add a parallel collapse path — see [2026-07_body-presentation-authority.md](../2026-07_body-presentation-authority.md) (shipped).

## Start here

- `Assets/Scripts/SS3D/Systems/Health/HumanHealthController.cs` — server tick, damage/treatment, organ registration, snapshot SyncVar + `SnapshotChanged`
- `Assets/Scripts/SS3D/Systems/Health/BodyPresentationIntent.cs` — snapshot → `BodyPresentationState` (Collapsed / Locomotion / Dead mapping)
- `Assets/Scripts/SS3D/Systems/Health/HealthScreenEffectMapper.cs` — local-owner snapshot → `ScreenEffectsSubSystem` intensities
- `Assets/Scripts/SS3D/Systems/Health/HealthAlertStackMapper.cs` — snapshot → health alert signals (no MainHud types); Main HUD merges with atmos
- `Assets/Scripts/SS3D/Systems/Health/HealthEnvironmentExposure.cs` / `HealthEnvironmentState.cs` — turf breathability, env burn, breath moles
- `Assets/Scripts/SS3D/Systems/Health/AtmosAlertStackMapper.cs` / `AtmosScreenEffectMapper.cs` — synced Environment → alerts / HotRoom/Cold/Fire screens
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/AtmosDamageCommand.cs` — `atmosdamage [on|off|status]`
- `Assets/Scripts/SS3D/Systems/Health/HumanAnatomyController.cs` — limb severance visuals, world drops, head mind-swap (Phase 5b)
- `Assets/Scripts/SS3D/Systems/Health/HealthSimulation.cs` — pool math, severity/bleeding, critical/death evaluation
- `Assets/Scripts/SS3D/Systems/Health/OrganSimulation.cs` — zone→organ damage, organ tick drains, perfusion, limb multipliers
- `Assets/Scripts/SS3D/Systems/Health/OrganInstance.cs` — organ registration on Human prefab / organ item prefabs
- `Assets/Scripts/SS3D/Systems/Health/Interactions/BandageInteraction.cs` — stop bleeding (Phase 1)
- `Assets/Scripts/SS3D/Systems/Health/WoundVfx.cs` — per-zone bleed particles / body decals; anchors prefer armature `ZoneTargetCollider` bones; particle/decal intensity and floor drip cadence scale with snapshot bleed rates
- `Assets/Scripts/SS3D/Systems/Health/HealthSnapshot.cs` — synced vitals + `BleedingRatePacked` / `TotalBleedingRate` for client VFX
- `Assets/Scripts/SS3D/Systems/Health/BloodDecalSpawner.cs` — pooled URP floor blood decals
- `Assets/Scripts/SS3D/Systems/Health/BleedingVfxCatalog.cs` — Resources catalog; tints white mask splatters for Decal `Base_Map`
- `Assets/Scripts/SS3D/Rendering/URP/DecalRenderingLayers.cs` — floor vs character DecalProjector rendering-layer masks
- `Assets/Content/WorldObjects/World/VFX/Health/BloodDecal.mat` + `BloodFloorDecal.prefab` — URP Decal assets (keep **Opaque**)
- `Assets/Scripts/SS3D/Systems/Health/Interactions/BurnDressingInteraction.cs` — zone burn heal (Phase 5)
- `Assets/Scripts/SS3D/Systems/Health/Interactions/SplintInteraction.cs` — limb splint (Phase 5)
- `Assets/Scripts/SS3D/Systems/Health/Interactions/OxygenTreatmentInteraction.cs` — oxy debt relief (Phase 5)
- `Assets/Scripts/SS3D/Systems/Health/Interactions/CprInteraction.cs` + `HandCprExtension.cs` — chest CPR (Phase 5)
- `Assets/Scripts/SS3D/Systems/Health/Interactions/TransfusionInteraction.cs` / `AntitoxinInteraction.cs` — medkit systemic treatments (Phase 5)
- `Assets/Scripts/SS3D/Systems/Health/Interactions/DefibrillatorInteraction.cs` — chest-zone defibrillation (Phase 3)
- `Assets/Scripts/SS3D/Systems/IngameConsoleSystem/Commands/DefibCommand.cs` — admin defib testing
- `Assets/Scripts/SS3D/Systems/Health/HealthDebugController.cs` — IMGUI overlay (H) for full zone/organ/pool inspection
- `Assets/Scripts/SS3D/Systems/Health/HealthDebugDetail.cs` — per-zone/per-organ SyncVar payload for debug UI
- `Assets/Scripts/SS3D/Systems/Health/ZoneTargetResolver.cs` — point + combat/hover raycast zone resolution, groin banding, reticle labels
- `Assets/Scripts/SS3D/Systems/Health/HealthLayers.cs` — `BodyParts` layer mask for combat raycasts

## Extension points

- `IHealthEffectModifier` — virology/chemistry/stamina deltas (Phase 7+)
- `ApplyDamage(BodyZone, MeleeDamagePacket)` — melee combat input (`MeleeDamagePacket` lives in Health)
- `ApplyDamage(BodyZone, brute, burn)` — direct damage (console `hurt`, future sources)
- `ApplyTreatment(...)` — zone treatments including splint flag (Phase 5)
- `ApplyBloodTransfusion` / `ApplyOxyRelief` / `ApplyAntitoxin` / `ApplyCpr` — systemic field treatments (Phase 5)
- `ZoneTargetResolver.TryResolveCombatZone` — zone raycast + groin banding for Harm hits (`ZoneTargetCollider` is the contract; bones may be on Characters)
- `ZoneTargetResolver.TryResolveHoverZone` / `GetReticleLabel` / `IsMeleeZoneReachInRange` — Main HUD + combat (exclude-self overload; AnatomyNode limb meshes; closest-point melee reach). Hover/hitscan accept optional `maxRayDistance` (default 8 m; ranged passes weapon max range).
- `GetZoneBruteFraction(BodyZone)` — 0..1 zone brute for gait/limp presentation (replaces legacy `FootBodyPart.RelativeDamage`)
- Screen feedback: [screen-effects](screen-effects.md) via `HealthScreenEffectMapper` + hit-flash TargetRpc — do not reimplement Volume overlays in Health.
- Personal heartbeat cue: [audio](audio.md) via `HealthPersonalAudioMapper` (`PersonalAudioSubSystem`) — driven from the same `ApplyScreenEffectsFromSnapshot`/`ClearScreenEffectsIfDriving` local-owner hooks as screen effects, not a separate subscription. Deliberately treats `IsCardiacArrest` as silence (opposite of the screen vignette) since the cue represents an actual beating heart.
- Alert stack: emit via `SnapshotChanged`; map with `HealthAlertStackMapper` only — [inventory](inventory.md) Main HUD owns `AlertStackState` / icon rendering.
- Body presentation: map vitals with `BodyPresentationIntent.FromSnapshot` and call `Ragdoll.ServerSetPresentation` — never apply ragdoll/animator visuals from Health.

## Pitfalls

- **Blood decals vanish after setting Surface Type Transparent:** URP `Decal` shadergraph materials must stay **Opaque**. Alpha already blends via the DBuffer/Screen Space passes (`SrcAlpha OneMinusSrcAlpha`); flipping `_Surface` / render queue 3000 / `_SURFACE_TYPE_TRANSPARENT` makes projectors stop drawing. Keep `BloodDecal.mat` Opaque like the package `Decal.mat` default.
- **White / untinted splatters:** most `Assets/Art/Textures/World/VFX/Splatters/Splatter*.png` are white masks. URP Decal has no Base Color multiply (only `Base_Map` → albedo). `BleedingVfxCatalog.CreateDecalMaterial` tints those masks at runtime; do not “fix” white by switching the material to Transparent.
- **Floor blood paints through the player / looks cut off:** Mesh Bias only affects mesh decals. Cutoffs: align with `DecalRotationOntoSurface` (never bare `LookRotation(-normal)` on flats). Through-player: URP Decal Layers need receivers to write rendering layers in a **DepthNormals** pass — Simple Toon lacked that, so character pixels kept the floor’s layer and still matched floor projectors. `STDefault` now has DepthNormals/`_WRITE_RENDERING_LAYERS`; tiles stamp `ReceiveWorldDecals`, floor projectors use `WorldFloorProjectorMask`.
- **Bleed particles float beside the limb:** do not parent VFX to `AnatomyNode` roots first — those prefab pivots do not follow the skinned mesh. Prefer `ZoneTargetCollider` bone transforms (see `WoundVfx.EnsureAnchors`).
- **Death re-triggers every health tick:** `TickHealth` must latch death (`_deathTriggered`) and stop ticking; otherwise `Human.Kill()` re-runs every second (ghost spam / dispose races). `WoundVfx` also clears and disables on `HealthState.Dead`.
- **Ghost spawn stack-overflows the editor:** `HumanoidGhostController.OnAwake` must call `base.OnAwake()`, never `base.Awake()` — the latter re-enters `NetworkActor.Awake` → `OnAwake` forever when `Human.Kill()` instantiates the ghost.
- **Death / collapse presentation:** `Ragdoll` owns replicated `BodyPresentationState`. Health must not call collapse visuals or reinforce RPCs. Latch health-owned collapses (`_healthCollapseActive`) so waking does not clear combat timed knockdown. Critical and cardiac arrest collapse even while `IsConscious` is still true. Do not rely on SyncVar OnChange alone — see [body-presentation-authority](../2026-07_body-presentation-authority.md).
- **Screen-effect/personal-audio Clear from other bodies:** only clear when `_drivingLocalPresentation` — other players' mind unassign must not wipe the local owner's Volume intensities or heartbeat cue.
- **Host alert/screen gap:** raise HUD consumers from `PublishSnapshot` as well as SyncVar OnChange — FishNet may skip OnChange on server assigns (same reason screen effects apply in `PublishSnapshot`).
- **No tile/atmos yet ≠ vacuum:** `SampleEnvironmentAtBody` returns `HealthEnvironmentState.SafeDefault` (breathable) when Tile/Atmos aren't ready — do not treat missing samples as vacuum or lobby spawns suffocate. `atmosdamage off` forces SafeDefault every tick.
- **Melee self-hit / missed limbs:** connect and reticle must pass `excludeHealth` (attacker) into `TryResolveHoverZone`; include detachable `AnatomyNode` mesh colliders and check reach with `IsMeleeZoneReachInRange` (closest point), not the ray impact alone — see [combat](combat.md).

## Depends on / Used by

- **Depends on:** [entities](entities.md), [interactions-framework](interactions-framework.md), [screen-effects](screen-effects.md), [audio](audio.md) (`PersonalAudioSubSystem`)
- **Used by:** [combat](combat.md) (melee zone hits), [inventory](inventory.md) (Main HUD alert stack), dev console `hurt`/`heal`, `HumanoidLivingController` / `HumanoidPredictedMovement` / `HumanoidBodyStateBridge` (movement multiplier / limp + `InjuredLeg` / injured-arm presentation), `Hand` (arm debuff stub)
- **Stamina:** [stamina](stamina.md) Phase 7a core — regen/encumbrance/overdraw→oxy; combat drains deferred

## Related docs

- Design (read-only): [Documents/design/health.md](../../design/health.md), [main-hud.md](../../design/main-hud.md) §9, [stamina.md](../../design/stamina.md), [armor.md](../../design/armor.md)
- Anatomy map: [health-anatomy-map.md](health-anatomy-map.md)
- Plan: [health_implementation_plan.md](../../plans/health_implementation_plan.md)
- Stamina map: [stamina](stamina.md)
- [2026-07_body-presentation-authority](../2026-07_body-presentation-authority.md) — **shipped** single authority for collapse/death presentation
- [2026-07_animation-polish](../2026-07_animation-polish.md) — limp/injured gait, severity idle, arm overlay, left-hand mirror
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- [screen-effects](screen-effects.md)
