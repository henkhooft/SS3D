# Architecture index

Navigation hub for agents. Read this before broad code search. Open the relevant [system map](systems/) for entry points and key files.

Authoring conventions: [Documents/SKILL.md](../SKILL.md). Agent rules: [AGENTS.md](../../AGENTS.md).
Cross-cutting structural risk register: [TECH_DEBT.md](TECH_DEBT.md).
Playable focus and dependency trees: [Documents/milestones/INDEX.md](../milestones/INDEX.md).

## Coverage table

One row per gameplay domain that has (or should eventually have) a design doc. **Design**
links to [`Documents/design/`](../design/) (owner-authored spec). **Architecture** links to
the effort doc(s) in this directory that implement it, or "none yet." **System map** links
to [`systems/`](systems/), or "none yet."

This is the answer to "what's left" at the domain level: a design with no architecture
entry is designed but unbuilt; a domain with no design entry hasn't been designed at all.
Feature-level gaps *within* an already-designed system stay in that design doc's own
`§Out of scope for this pass` — this table doesn't duplicate those. **Sequenced playable
gates and what to focus on next** live in [Documents/milestones/](../milestones/), not here.
Update as part of `update-system-docs`.

| Domain | Design | Architecture | System map |
|---|---|---|---|
| main-hud | [main-hud.md](../design/main-hud.md) — active | [phase1-foundation](2026-07_machine-interface-phase1-foundation.md), [phase2-apc-networking](2026-07_machine-interface-phase2-apc-networking.md), [phase3-smes-generalization](2026-07_machine-interface-phase3-smes-generalization.md), [diegetic-screen-ui-framework](2026-07_diegetic-screen-ui-framework.md), [mi-area-electricity-debt](2026-07_mi-area-electricity-debt.md), [screen-space-effects](2026-07_screen-space-effects.md), [mi-path-catalog](2026-07_mi-path-catalog.md) — all shipped (screen-effects health + turf temp/fire wired; UiShell still deferred); player HUD overlay is a partial in-branch slice (no dated effort yet) | [inventory](systems/inventory.md) — partial (player HUD); also [machine-interface](systems/machine-interface.md), [screen-effects](systems/screen-effects.md) |
| comms | [comms.md](../design/comms.md) — active | [comms-non-diegetic-feed](2026-07_comms-non-diegetic-feed.md) — shipped (feed + Tab/slash compose + announce SFX + Chat purge; headset/PDA/radial deferred) | [chat-audio-screens](systems/chat-audio-screens.md) — partial |
| area | [area.md](../design/area.md) — active | [area-foundation](2026-07_area-foundation.md) — shipped (partial: APC-seeded variant; live mutation recompute and editor merge/split deferred) | [area](systems/area.md) — partial |
| hacking-interface | [hacking-interface.md](../design/hacking-interface.md) — active | none yet | none yet |
| combat | [combat.md](../design/combat.md) — active | [player-body-animation](2026-07_player-body-animation.md) — shipped (stance/locomotion); [animation-polish](2026-07_animation-polish.md) — shipped (melee torso, limp, mirror, swing variants); [combat_implementation_plan](../plans/combat_implementation_plan.md) — Phase 0–1 melee + Phase 3 ranged hitscan + Phase 4 stamina drains (swing/fire; block deferred) + Phase 5 armor absorption shipped; Phases 2/6–7 pending; M1p feel subset (muzzle/SS14 audio/anims/two-hand/holes/`rangeddebug`) shipped, aim IK deferred | [combat](systems/combat.md) — partial |
| stamina | [stamina.md](../design/stamina.md) — active | [2026-07_inventory-storage-redesign](2026-07_inventory-storage-redesign.md) — Phase 7a core shipped with inventory clean-slate; combat plan Phase 4 swing/fire drains + exertion feedback (accuracy cone, windup/recovery) shipped, block drain deferred | [stamina](systems/stamina.md) — partial |
| health | [health.md](../design/health.md) — active | rewrite in flight: [health_implementation_plan](../plans/health_implementation_plan.md); [body-presentation-authority](2026-07_body-presentation-authority.md) — shipped; [health-env-feel](2026-07_health-env-feel.md) — shipped (turf exposure + feel SFX); screen overlays in [screen-space-effects](2026-07_screen-space-effects.md) (health + turf temp/fire); Main HUD alert stack health+atmos-wired; examine Tier 0/1 shipped (vitals UITK still open) | [health](systems/health.md) — partial |
| armor | [armor.md](../design/armor.md) — active | [combat_implementation_plan](../plans/combat_implementation_plan.md) Phase 5 — combat-armor absorption shipped (per-zone flat brute/burn absorption + integrity); environmental seal/breach (§3) deferred (turf→health exposure exists via [health-env-feel](2026-07_health-env-feel.md); seal still open) | [combat](systems/combat.md) — partial (armor absorption); no dedicated armor map, folded into combat |
| inventory-storage | [inventory-storage.md](../design/inventory-storage.md) — active | [2026-07_inventory-storage-redesign](2026-07_inventory-storage-redesign.md) — in-progress (clean-slate: data model + panel + Main HUD equip/drag + stamina 7a + old UI purge shipped; clothing folded world presentation shipped; Play Mode verification pending) | [inventory](systems/inventory.md) — partial |
| examine | [examine.md](../design/examine.md) — active | none yet | [examine](systems/examine.md) — partial (character-examine §7 + health Tier 0/1 + Search paperdoll + interruptible hold-to-take on UITK; obscured-slot filter + restrained loot + StoragePanel foreign-loot still open) |
| crafting | [crafting.md](../design/crafting.md) — active | none yet | [crafting](systems/crafting.md) — stub (obsolete runtime purged; awaiting redesign) |
| death-cloning-respawn | [death-cloning-respawn.md](../design/death-cloning-respawn.md) — active | none yet | none yet |
| surgery | [surgery.md](../design/surgery.md) — active | none yet | none yet |
| lobby | [lobby.md](../design/lobby.md) — active | none yet | [rounds-lobby](systems/rounds-lobby.md) — shipped |
| round-config | [round-config.md](../design/round-config.md) — active | none yet | [rounds-lobby](systems/rounds-lobby.md) — shipped |
| round-end | [round-end.md](../design/round-end.md) — active | none yet | none yet |
| observer | [observer.md](../design/observer.md) — active | none yet | none yet |
| electricity | [electricity.md](../design/electricity.md) — active | [solar-generation](2026-07_solar-generation.md) — shipped (panels + tracker + stub cycle; reactor / real celestial deferred); earlier touch via [mi-area-electricity-debt](2026-07_mi-area-electricity-debt.md) (implements area.md/main-hud.md, not electricity.md) | [electricity](systems/electricity.md) — partial |
| pda | [pda.md](../design/pda.md) — active | none yet | [inventory](systems/inventory.md) — partial |
| cargo | [cargo.md](../design/cargo.md) — active | none yet | none yet |
| disposal | [disposal.md](../design/disposal.md) — active | [disposal-item-network](2026-07_disposal-item-network.md) — shipped (item network; pipe craft, Cargo, player transit deferred) | [disposal](systems/disposal.md) — partial |
| id-access | [id-access.md](../design/id-access.md) — active | none yet | [id-access](systems/id-access.md) — partial |
| virology | [virology.md](../design/virology.md) — active | none yet | none yet |
| atmospherics | [atmospherics.md](../design/atmospherics.md) — active | [atmos-ecs-foundation](2026-07_atmos-ecs-foundation.md) — shipped (partial; equal-P composition diffusion follow-on); [atmos-client-visualization-sync](2026-07_atmos-client-visualization-sync.md) — shipped (Phase 1 dirty-chunk; Phase 2 late-join/AOI open; Phase 3 host AOI atlas partial); [metastation-scale-perf](2026-07_metastation-scale-perf.md) — shipped | [atmospherics](systems/atmospherics.md) — partial |
| chemistry | [chemistry.md](../design/chemistry.md) — active | [substances-foundation](2026-07_substances-foundation.md) — shipped (primitives; gameplay / MVP2 deferred) | [substances](systems/substances.md) — partial |
| explosives-destruction | [explosives-destruction.md](../design/explosives-destruction.md) — active | [structural-destruction](2026-07_structural-destruction.md) — in-progress (Phase 1–4 + blast detonation VFX) | [structural-destruction](systems/structural-destruction.md) — partial |
| construction | [construction.md](../design/construction.md) — active | none yet | [tile](systems/tile.md) — partial (staged build ladder §1-2 unimplemented; single-step placement only) |
| creative-mode | [creative-mode.md](../design/creative-mode.md) — active | [spawn-point-authoring](2026-07_spawn-point-authoring.md) — shipped (authoring + save; runtime resolution deferred); map editor foundation in [map-editor-replacement](2026-07_map-editor-replacement.md); [ss13-map-import](2026-07_ss13-map-import.md) — shipped (floors/walls/windows/doors shell + gap report); [ss13-map-import-infrastructure](2026-07_ss13-map-import-infrastructure.md) — shipped (cables/pipes/disposals/vents/scrubbers/APCs/lights; follow-on lattices/tables/SMES); [metastation-scale-perf](2026-07_metastation-scale-perf.md) — shipped (Map Editor sim suspend + play AOI) | [tile](systems/tile.md) — partial (Spawn Placements + DMM import); [map-editor-creative-hooks](systems/map-editor-creative-hooks.md) |
| rendering-lighting | [rendering-lighting.md](../design/rendering-lighting.md) — active | look pass planned: [urp_lighting_look_plan](../plans/urp_lighting_look_plan_d42c32f5.plan.md); polish handoff [2026-07_urp-lighting-look-polish](2026-07_urp-lighting-look-polish.md) (planned); palette emission sample fix shipped on Simple Toon; [srp-batcher-gpu-instancing](2026-07_srp-batcher-gpu-instancing.md) — shipped (floor + GenericShadeless instancing; Content probes Off; selection MPB off MeshRenderers); SSAO off on Forward+ (DepthNormals kept for Decal Layers); [unity-framedebug-ai-tooling](2026-07_unity-framedebug-ai-tooling.md) — shipped | [rendering](systems/rendering.md) — partial |
| shuttles | [shuttles.md](../design/shuttles.md) — active | none yet | none yet |
| ai-cyborgs | [ai-cyborgs.md](../design/ai-cyborgs.md) — active | none yet | none yet |
| objectives | [objectives.md](../design/objectives.md) — active | none yet | none yet |
| persistence-save | [persistence-save.md](../design/persistence-save.md) — active | [persistence_architecture_design_2fe61864.plan.md](../plans/persistence_architecture_design_2fe61864.plan.md) — Phase 1a/1b shipped, Phase 2 round snapshots pending | [persistence](systems/persistence.md) — partial (station templates, server meta only) |
| networking | [networking.md](../design/networking.md) — active | [headless-dedicated-server](2026-07_headless-dedicated-server.md) — shipped (partial: selection outline and drop interaction against a real client still broken, not root-caused), [multiplayer-test-harness](2026-07_multiplayer-test-harness.md) — shipped (partial: mouse/screen-space interaction and pocket/container regressions not covered), [ci-develop-release-pipeline](2026-07_ci-develop-release-pipeline.md) — shipped (manual Windows+bats by default; nightly Windows+Linux client/server → `develop-nightly`; Linux/EditMode/smoke opt-in on dispatch), [session-world-lifecycle](2026-07_session-world-lifecycle.md) — shipped, [multiplayer-testing-self-hosted-ci](2026-07_multiplayer-testing-self-hosted-ci.md) — in-progress (TomNAS-unity online; EditMode + develop-release TomNAS+GitHub fallback; warm-run proof open; Phases 1–2 pending) | [networking-session](systems/networking-session.md) — partial |
| audio | [audio.md](../design/audio.md) — active | [audio-foundation](2026-07_audio-foundation.md) — in-progress (Phase 1 SFX occlusion + Phase 2 ambience + Phase 3 personal heartbeat/breathing + Phase 4 alert cues shipped; Phase 0/5 pending) | [audio](systems/audio.md) — partial (split from chat-audio-screens) |
| onboarding-tutorial | none yet | none yet | none yet |
| antagonist-content | [antagonist-content.md](../design/antagonist-content.md) — active | none yet | [gamemodes-roles-traits](systems/gamemodes-roles-traits.md) — stub |
| rd-material-economy | none yet | none yet | none yet |
| cryogenics | [cryogenics.md](../design/cryogenics.md) — active | none yet | none yet |
| admin-tools | [admin-tools.md](../design/admin-tools.md) — active | none yet | [ingame-console](systems/ingame-console.md) — partial (dev/admin console, not a design spec) |
| player-accounts | [player-accounts.md](../design/player-accounts.md) — active | none yet | none yet |

[2026-07_interaction-system-hardening](2026-07_interaction-system-hardening.md) /
[2026-07_interaction-discover-contract](2026-07_interaction-discover-contract.md) /
[2026-07_interaction-controller-decomposition](2026-07_interaction-controller-decomposition.md) (shipped) and the infrastructure systems below (core
subsystems, rendering pipeline internals, data/codegen, etc.) aren't gameplay domains with
their own design docs — they support the domains above rather than being one themselves,
so they stay out of this table and live only in the Infrastructure section below.

### Migration note

`design/round-end.md` (round-end/transition) is now **active** — it builds its summary screen and
transition on the spectator framework `observer.md` §7 supplies, spends the secret gamemode identity
`round-config.md` §4 protects, and defines the evac shuttle's call/countdown/point-of-no-return
sequence (§3), closing the dependency `shuttles.md` §9 and `objectives.md` §7 both flagged against
it. Authored in the house style (no prototyping section, design-docs-cite-design-docs only, numbered
Design Philosophy/Worked Examples/Integration Notes/Out of Scope matching every other design doc).

## Infrastructure

| System | Map | Status | Summary |
|--------|-----|--------|---------|
| Core / SubSystems | [core-subsystems](systems/core-subsystems.md) | partial | `SystemsBootstrap` DDOL + `NetworkSystemsHub` Online; `IWorldReady` / readiness graph; locator silent while WaitingForServer |
| Application | [application](systems/application.md) | stub | `ApplicationInitializerSubSystem` via SystemsBootstrap (DDOL); Boot is launch pad only |
| Networking (session) | [networking-session](systems/networking-session.md) | partial | SessionState FSM + Empty offline; NetworkSystemsHub holds Game SubSystems; harness + headless |
| Scene management | [scene-management](systems/scene-management.md) | stub | SceneSubSystem DDOL; `Scenes.Empty` offline after first Online |
| UI shell | [ui-shell](systems/ui-shell.md) | partial | UITK composition root; `UiShellSubSystem` + shared catalog/animator/binder scaffolding shipped, radial + armed migrated; MI/Main HUD path catalogs still separate (duplicated), migration deferred |
| Interactions (framework) | [interactions-framework](systems/interactions-framework.md) | shipped | Shared `IInteraction` contracts, Discover/`HasPoint` contract, pipeline, wire identifiers; InteractionIcons via AddressablesAsync preload |
| Data / codegen | [data-codegen](systems/data-codegen.md) | partial | Asset databases + generated refs; InteractionIcons on Addressables async path; other DBs still eager; catalog rebuild menus still debt — [2026-07_addressables-expansion-migration](2026-07_addressables-expansion-migration.md) Phases 1–3 done |
| Persistence | [persistence](systems/persistence.md) | partial | Station templates + server meta; restore resets world-readiness epoch and notifies TileMapLoaded |
| Localization | [localization](systems/localization.md) | partial | `LocalizedTextService` and examine string tables |
| Logging | [logging](systems/logging.md) | shipped | Serilog structured logging |
| Permissions | [permissions](systems/permissions.md) | partial | Admin permission checks; persisted via [persistence](systems/persistence.md) envelope with legacy txt fallback |
| Rendering | [rendering](systems/rendering.md) | partial | URP features: selection pick, atmos VFX, Vision FOV; Decal Layers + DBuffer blood; SSAO off (DepthNormals kept for decals); ST + GenericShadeless instancing / Content probes Off; Frame Debugger → `Logs/framedebug/` |
| Asset organization | [asset-organization](systems/asset-organization.md) | partial | Art/Content/Data folder taxonomy; Phase 1 icons under `Art/Icons/`; InteractionIcons + remaining Phase 1 leftovers open; audit in [2026-07_asset-file-structure-taxonomy](2026-07_asset-file-structure-taxonomy.md) |

## Gameplay

| System | Map | Status | Summary |
|--------|-----|--------|---------|
| Interactions (runtime) | [interactions-runtime](systems/interactions-runtime.md) | shipped | Thin `InteractionController` router + discovery/dispatch/outline helpers; Harm via sibling `CombatInteractionNetwork`; radial/armed/outlines; intent↔stance (**F**); Backspace cancel |
| Selection | [selection](systems/selection.md) | shipped | Shader-ID mesh picking; 4 m pick-grid spatial index + cursor-ray/frustum cull; outline shells excluded from pick pass |
| Examine | [examine](systems/examine.md) | partial | Hover/detailed examine on UITK; Search paperdoll + take; health Tier 0/1 lines + Shift→self fallback |
| Tile / construction | [tile](systems/tile.md) | partial | Tilemap/adjacency; Map Editor (sim suspend while open); underfloor MeshRenderer occlusion (play); SS13 DMM import (structural + cables/pipes/disposals/fixtures); end-of-restore → TileMapLoaded (not OnMapCreated); staged build ladder unbuilt |
| Atmospherics | [atmospherics](systems/atmospherics.md) | partial | ECS turf gas sim (pressure share + equal-P composition diffusion); awaits TileMapLoaded / notifies AtmosReady; client VFX Phase 1 dirty-chunk sync; host AOI atlas upload; late-join/AOI Phase 2 open |
| Area | [area](systems/area.md) | partial | APC flood-fill; notifies AreasFlooded; client lighting snapshot + floor-cache area ids |
| Electricity | [electricity](systems/electricity.md) | partial | kWh / HV grid / APC; O(1) `_circuitByDevice`; solar panels + tracker (stub `SolarCycle`); awaits AreasFlooded → ElectricityReady; client LightPower SyncVar; Pacman vibrate captures rest yaw on enable |
| Substances | [substances](systems/substances.md) | partial | Volume-first networked containers + reaction/heat foundation shipped — [substances-foundation](2026-07_substances-foundation.md); chemistry gameplay deferred |
| Inventory | [inventory](systems/inventory.md) | partial | Items/containers/hands + weight/size-class/stacking/locks; clothing folded world form (`ClothingItemPresentation`) vs worn body mesh; HUD icons via `IconPreviewGenerator` / ObjectIcon; Main HUD sole equip/storage UI + StoragePanel + zone reticle (lock-on recharge + connect flash + ranged bloom; ScreenToPanel cursor + pivot-centered hit flash) + intent chip (polls `CurrentIntent`); HUD suppressed while MI open; old uGUI purged; `CarriedWeight` → stamina; Human hands wiring now recipe-managed (`HandsPrefabSetup`) |
| Stamina | [stamina](systems/stamina.md) | partial | Phase 7a core: health-modulated regen, encumbrance, sprint drain, overdraw→oxy; no permanent bar; combat swing/fire drains + exertion feedback (accuracy cone, windup/recovery) shipped; block drain deferred |
| Entities | [entities](systems/entities.md) | partial | Humanoids, minds, spawning; body-state animation + combat stances (`RangedWeaponItemExtension` preferred for Ranged) + Ranged Upper Body Rifle Aim Idle + Fire/Reload oneshots + injured limp/severity/mirror; living space float via `HumanoidSupportState` (client AOI Unknown ≠ float; server SyncVar for deep space) + `Mix_Floating`; Harm intent → combat stance; shelved Misc/Probably Not clips; `Human.prefab` composition debt; body presentation via `Ragdoll`/`BodyPresentationState` ([body-presentation-authority](2026-07_body-presentation-authority.md) shipped) |
| Health | [health](systems/health.md) | partial | Phases 1–5b + env-feel shipped; examine Tier 0/1 shipped; vitals UITK Phase 6 remainder; collapse via `BodyPresentationIntent` → `Ragdoll`; limp/`InjuredLeg`/arm injury → body anim |
| Combat | [combat](systems/combat.md) | partial | Harm primary on `CombatInteractionNetwork` (sibling of `InteractionController`); Phase 0–1 melee + Phase 3 ranged hitscan (M4, accuracy cone, shared LOS, mag/reload) + Phase 4 stamina drains/exertion feedback (swing+fire; block deferred) + Phase 5 per-zone armor absorption/integrity; M1p feel subset shipped; aim IK / blood / knockdown / sprint / strip debug chrome still open; disarm/blocking/projectile/environmental seal deferred |
| Crafting | [crafting](systems/crafting.md) | stub | Obsolete runtime purged (TECH_DEBT 1.6); awaiting redesign per design doc |
| Furniture / world objects | [furniture](systems/furniture.md) | partial | Airlocks (HashGrid proximity + SyncVar animator, no NetworkAnimator + Open/Close + access-denied), lockers, vendors, jukebox; disposal → [disposal](systems/disposal.md); vending via diegetic machine-interface |
| Structural destruction | [structural-destruction](systems/structural-destruction.md) | partial | Phase 1–4: Turf integrity stages; melee + ranged StructuralForce; blast BFS + cascade; Destroyed→clear; Cracked airtightness; Area deferred live reflood; MPB stage tint + Cracked hiss; examine; epicenter blast VFX (fireball/light/scorch/shake/flash); `hurtstructure` / `blast` |
| Disposal | [disposal](systems/disposal.md) | partial | Item disposal network; awaits TileMapLoaded → DisposalReady; pipe craft / Cargo / player transit deferred |
| Rounds / lobby | [rounds-lobby](systems/rounds-lobby.md) | shipped | Round state machine; PrepareRound awaits WorldReady; lobby UI condemned |
| Gamemodes / roles / traits | [gamemodes-roles-traits](systems/gamemodes-roles-traits.md) | stub | Objectives, job roles, character traits |
| Player control | [player-control](systems/player-control.md) | partial | Player subsystem, connect/authorize/disconnect lifecycle (incl. reconnect-to-body), and input routing |
| Chat / screens | [chat-audio-screens](systems/chat-audio-screens.md) | partial | Local speech + T-compose (Tab + `/eng`/`/sec`/`/announce`); left feed (radio-tower icon) + top announce banner + chime sequence; Chat hub purged; camera ownership planned ([camera-ownership](2026-07_camera-ownership.md)) |
| Audio | [audio](systems/audio.md) | partial | Networked SFX/music pool (`AudioSubSystem`); client-local occlusion (`AudioSourceOcclusion`, seventh `LineOfSight` consumer); per-area ambience crossfade (`AmbienceSubSystem`) off synced `AreaRecord.AmbienceTrackId`; personal heartbeat/breathing/alert cue (`PersonalAudioSubSystem` + Health/Stamina/AlertStack mappers — clips registered); health hit/gasp/blood one-shots; Boombox/NoisyCollision; legacy `AmbienceHandler` superseded |
| Machine interface UI | [machine-interface](systems/machine-interface.md) | shipped | Diegetic APC/SMES/atmos/vending; path catalog; Dual Kawase blur + dim; DOTween bring-up/dismiss |
| Screen-space effects | [screen-effects](systems/screen-effects.md) | partial | URP Volume overlays; health drives dying/blood/oxy/concussion/unconscious + hit flash; turf temp/fire via `AtmosScreenEffectMapper`; `SetUiBackdropBlur` for machine UI; F2 debug Canvas condemned |
| ID / access | [id-access](systems/id-access.md) | partial | Crew records, credential checks, doors, machine UI gates, dev console helpers |
| Inputs | [inputs](systems/inputs.md) | partial | Arbitration + default scheme ([2026-07_default-input-scheme](2026-07_default-input-scheme.md): Caps sprint, Shift examine, F intent, Backspace cancel); `InputInterface` UITK/uGUI pointer authority |
| In-game console | [ingame-console](systems/ingame-console.md) | partial | Command dispatch; console panel uGUI condemned pending UITK debug layer |

## Architecture efforts (dated)

Implementation history — not navigation maps. Update `Status` in the header when an effort ships.

| Effort | Status |
|--------|--------|
| [2026-07_machine-interface-phase1-foundation](2026-07_machine-interface-phase1-foundation.md) | shipped |
| [2026-07_machine-interface-phase2-apc-networking](2026-07_machine-interface-phase2-apc-networking.md) | shipped |
| [2026-07_machine-interface-phase3-smes-generalization](2026-07_machine-interface-phase3-smes-generalization.md) | shipped |
| [2026-07_diegetic-screen-ui-framework](2026-07_diegetic-screen-ui-framework.md) | shipped |
| [2026-07_interaction-system-hardening](2026-07_interaction-system-hardening.md) | shipped |
| [2026-07_interaction-discover-contract](2026-07_interaction-discover-contract.md) | shipped |
| [2026-07_interaction-controller-decomposition](2026-07_interaction-controller-decomposition.md) | shipped |
| [2026-07_area-foundation](2026-07_area-foundation.md) | shipped (deferred: live mutation recompute, editor merge/split) |
| [2026-07_atmos-ecs-foundation](2026-07_atmos-ecs-foundation.md) | shipped (deferred: liquid/solid phase; equal-P composition diffusion follow-on shipped; client VFX is a separate effort — Phase 1 shipped) |
| [2026-07_map-editor-replacement](2026-07_map-editor-replacement.md) | shipped |
| [2026-07_spawn-point-authoring](2026-07_spawn-point-authoring.md) | shipped (authoring + save; runtime resolution deferred) |
| [2026-07_ss13-map-import](2026-07_ss13-map-import.md) | shipped (floors/walls/windows/doors + unmapped report; SS14 deferred) |
| [2026-07_tile-overlay-replacement](2026-07_tile-overlay-replacement.md) | shipped |
| [2026-07_atmos-client-visualization-sync](2026-07_atmos-client-visualization-sync.md) | shipped (Phase 1; Phase 2 late-join/AOI open) |
| [2026-07_mi-area-electricity-debt](2026-07_mi-area-electricity-debt.md) | shipped |
| [2026-07_player-body-animation](2026-07_player-body-animation.md) | shipped (foundation; polish in [animation-polish](2026-07_animation-polish.md)) |
| [2026-07_animation-polish](2026-07_animation-polish.md) | shipped (melee torso, limp severity/oneshots, left-hand mirror, swing variants) |
| [2026-07_screen-space-effects](2026-07_screen-space-effects.md) | shipped (foundation + health + turf temp/fire) |
| [2026-07_headless-dedicated-server](2026-07_headless-dedicated-server.md) | shipped (partial: selection outline, drop interaction against a real client still broken, not root-caused) |
| [2026-07_agent-first-composition](2026-07_agent-first-composition.md) | shipped (policy); code deferred — subsystem bootstrap, prefab tooling; main-HUD UITK slice partial ([inventory](systems/inventory.md)) |
| [2026-07_mi-path-catalog](2026-07_mi-path-catalog.md) | shipped (MI path catalog wedge of composition follow-on b; Main HUD later copied the pattern — unify under [ui-shell](systems/ui-shell.md)) |
| [2026-07_ui-shell-consolidation](2026-07_ui-shell-consolidation.md) | shipped (Phase 0-1: `UiShellSubSystem` + shared scaffolding, radial/armed migration); MI/Main HUD/Storage Panel migration deferred to later phases |
| [2026-07_body-presentation-authority](2026-07_body-presentation-authority.md) | shipped |
| [2026-07_health-env-feel](2026-07_health-env-feel.md) | shipped (M7 feel + M8 exposure; seal/internals/alarms deferred) |
| [2026-07_inventory-storage-redesign](2026-07_inventory-storage-redesign.md) | in-progress (clean-slate + stamina 7a code shipped; Play Mode verification pending) |
| [2026-07_multiplayer-test-harness](2026-07_multiplayer-test-harness.md) | shipped (partial: mouse/screen-space interaction and pocket/container round-trip regressions not covered; not yet verified against a real Unity build) |
| [2026-07_ci-develop-release-pipeline](2026-07_ci-develop-release-pipeline.md) | shipped (manual workflow_dispatch Windows-default; nightly full cut → develop-nightly; Linux/EditMode/smoke opt-in; TomNAS prefer) |
| [2026-07_session-world-lifecycle](2026-07_session-world-lifecycle.md) | shipped |
| [2026-07_disposal-item-network](2026-07_disposal-item-network.md) | shipped (item network; pipe craft, Cargo, player transit deferred) |
| [2026-07_camera-ownership](2026-07_camera-ownership.md) | planned (dedicated camera manager / contexts; same ownership smell as pre-arbiter input) |
| [2026-07_input-arbitration](2026-07_input-arbitration.md) | shipped |
| [2026-07_default-input-scheme](2026-07_default-input-scheme.md) | shipped (Caps sprint, Shift examine, F intent, Backspace cancel; no Shift intent-override) |
| [2026-07_human-prefab-decomposition](2026-07_human-prefab-decomposition.md) | in-progress (Phase 0 hygiene + Phase 2 recipe convention + Phase 3 hands-wiring done and verified in-Editor; Phase 1 organs deprioritized; remaining Phase 3 domains scheduled not forced) |
| [2026-07_unity-perf-ai-tooling](2026-07_unity-perf-ai-tooling.md) | shipped (Editor Profiler → Logs/perf markdown + analyze-unity-perf skill; player capture / console / budgets deferred) |
| [2026-07_unity-framedebug-ai-tooling](2026-07_unity-framedebug-ai-tooling.md) | shipped (Editor Frame Debugger → Logs/framedebug markdown + analyze-unity-framedebug skill; Quick default / Full GPU-replay; remote attach deferred) |
| [2026-07_metastation-scale-perf](2026-07_metastation-scale-perf.md) | shipped (Map Editor sim suspend; airlock proximity invert; host atmos AOI atlas; overlay AOI; underfloor MeshRenderer occlusion) |
| [2026-07_srp-batcher-gpu-instancing](2026-07_srp-batcher-gpu-instancing.md) | shipped (ST floor + GenericShadeless instancing; Content light/reflection probes Off recipe; adjacency sharedMesh; selection pick without MeshRenderer MPBs; Intact integrity clears MPB; chunk mesh combine deferred) |
| [2026-07_structural-destruction](2026-07_structural-destruction.md) | in-progress (Phase 1–4 + blast detonation VFX) |
| [2026-07_addressables-expansion-migration](2026-07_addressables-expansion-migration.md) | in-progress (Phases 1–3 done: cleanup, AssetHandle/provider, InteractionIcons pilot; Phases 4–6 open) |
| [2026-07_asset-file-structure-taxonomy](2026-07_asset-file-structure-taxonomy.md) | planned (audit + taxonomy + phased plan written; Phase 0 docs/tooling + CI-enforced `AssetTaxonomyTests` landed, Phase 1+ file moves not started) |
| [2026-07_audio-foundation](2026-07_audio-foundation.md) | in-progress (Phase 1 client-local SFX occlusion + Phase 2 per-area ambience + Phase 3 personal heartbeat/breathing + Phase 4 alert-stack cues shipped; Phase 0 mixer groups, Phase 5 music/settings pending) |
| [2026-07_comms-non-diegetic-feed](2026-07_comms-non-diegetic-feed.md) | shipped (feed + Tab/slash compose + announce SFX + Chat purge; headset/PDA/radial deferred) |
| [2026-07_multiplayer-testing-self-hosted-ci](2026-07_multiplayer-testing-self-hosted-ci.md) | in-progress (TomNAS-unity online; EditMode + develop-release TomNAS+GitHub fallback; warm smoke proof open; Phases 1–2 pending; rendered-client + soak deferred) |
| [2026-07_editor-tooling-tiers](2026-07_editor-tooling-tiers.md) | shipped (A/B/C Editor menu tiers; demote one-shot PrefabSetup MenuItems to recipe aggregators; delete finished migrations; catalog rebuild pipeline still TECH_DEBT 1.4/1.5) |
| [2026-07_substances-foundation](2026-07_substances-foundation.md) | shipped (Phases 0–2: shared reagent defs, networked containers, reaction+heat; chemistry gameplay MVP2) |

## Implementation plans

Temporary working plans in [Documents/plans/](../plans/). Update todos when work ships.

| Plan | Topic |
|------|-------|
| [examine_localization_design_5ca361a6.plan.md](../plans/examine_localization_design_5ca361a6.plan.md) | Examine localization migration |
| [radial_menu_implementation_5a83bdf9.plan.md](../plans/radial_menu_implementation_5a83bdf9.plan.md) | Three-tier radial interaction menu (Phases 4–5 pending) |
| [interaction_system_improvements_9e14ae22.plan.md](../plans/interaction_system_improvements_9e14ae22.plan.md) | Interaction system hardening |
| [diegetic_screen_ui_framework_643c2e6f.plan.md](../plans/diegetic_screen_ui_framework_643c2e6f.plan.md) | Diegetic shell + vending (shipped) |
| [areas_implementation_plan_c0639343.plan.md](../plans/areas_implementation_plan_c0639343.plan.md) | APC-seeded areas, flood-fill, power/lighting follow-ups |
| [electricity_kwh_foundation_917ccdbc.plan.md](../plans/electricity_kwh_foundation_917ccdbc.plan.md) | kWh storage, priority shedding, HV cable grid rules |
| [persistence_architecture_design_2fe61864.plan.md](../plans/persistence_architecture_design_2fe61864.plan.md) | Layered persistence framework; Phase 1a/1b shipped, Phase 2 round snapshots pending |
| [tile_overlay_replacement.plan.md](../plans/tile_overlay_replacement.plan.md) | Area floor stripes + sparse floor decals; Overlays layer removed |
| [spawn_point_authoring.plan.md](../plans/spawn_point_authoring.plan.md) | Map Editor spawn markers + station-template save (runtime resolve deferred) |
| [animation_system_design_250de599.plan.md](../plans/animation_system_design_250de599.plan.md) | Player body / layered animation foundation (+ polish notes) |
| [health_implementation_plan.md](../plans/health_implementation_plan.md) | Clean-slate health rewrite (Phases 0–5b + examine Tier 0/1 shipped; vitals UITK + 7–9 pending) |
| [combat_implementation_plan.md](../plans/combat_implementation_plan.md) | Clean-slate combat: Phase 0–1 melee + Phase 3 ranged hitscan + Phase 4 stamina drains (swing/fire) shipped; disarm/armor/blocking later |
| [urp_lighting_look_plan_d42c32f5.plan.md](../plans/urp_lighting_look_plan_d42c32f5.plan.md) | URP half-toon look pass (pending) |
| [comms_non_diegetic_feed.plan.md](../plans/comms_non_diegetic_feed.plan.md) | Radio/announce feed + Tab/slash compose; Chat purge (shipped) |
| [station_structural_damage.plan.md](../plans/station_structural_damage.plan.md) | Structural integrity / blast / items (Phase 1–4 shipped; 5–6 pending) |
| [shuttle_system_design_a3dd2e04.plan.md](../plans/shuttle_system_design_a3dd2e04.plan.md) | Shuttle tile blueprints / multi-map (pending) |
| [substances_foundation_implementation.plan.md](../plans/substances_foundation_implementation.plan.md) | Substances clean-slate foundation (Phases 0–2 shipped; chemistry gameplay MVP2) |

## Design specs (read-only)

Gameplay specs in [Documents/design/](../design/) — owner-maintained. Agents link, never edit.
See the [coverage table](#coverage-table) above for design/architecture/system-map status
per domain.

## Reference (non-system)

| Resource | Path | Use when |
|----------|------|----------|
| Playable milestones | [milestones/INDEX.md](../milestones/INDEX.md) | What to focus on next; dependency trees toward MVP1/MVP2 playable gates |
| Technical debt tracker | [TECH_DEBT.md](TECH_DEBT.md) | Prioritizing structural risk / code smells before picking up cleanup work |
| Art asset index | [art-asset-index.md](../art-asset-index.md) | Locating or importing art from SS3D-Art |
| Available for import | [art-available-for-import.json](../art-available-for-import.json) | Finding game-ready art not yet in `Assets/Art/` |
| UI icon index | [icon-index.md](../icon-index.md) | Finding external game-icons SVGs for UI work |
