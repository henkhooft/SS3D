# Architecture index

Navigation hub for agents. Read this before broad code search. Open the relevant [system map](systems/) for entry points and key files.

Authoring conventions: [Documents/SKILL.md](../SKILL.md). Agent rules: [AGENTS.md](../../AGENTS.md).
Cross-cutting structural risk register: [TECH_DEBT.md](TECH_DEBT.md).

## Coverage table

One row per gameplay domain that has (or should eventually have) a design doc. **Design**
links to [`Documents/design/`](../design/) (owner-authored spec). **Architecture** links to
the effort doc(s) in this directory that implement it, or "none yet." **System map** links
to [`systems/`](systems/), or "none yet."

This is the answer to "what's left" at the domain level: a design with no architecture
entry is designed but unbuilt; a domain with no design entry hasn't been designed at all.
Feature-level gaps *within* an already-designed system stay in that design doc's own
`§Out of scope for this pass` — this table doesn't duplicate those. Update as part of
`update-system-docs`.

| Domain | Design | Architecture | System map |
|---|---|---|---|
| main-hud | [main-hud.md](../design/main-hud.md) — active | [phase1-foundation](2026-07_machine-interface-phase1-foundation.md), [phase2-apc-networking](2026-07_machine-interface-phase2-apc-networking.md), [phase3-smes-generalization](2026-07_machine-interface-phase3-smes-generalization.md), [diegetic-screen-ui-framework](2026-07_diegetic-screen-ui-framework.md), [mi-area-electricity-debt](2026-07_mi-area-electricity-debt.md), [screen-space-effects](2026-07_screen-space-effects.md), [mi-path-catalog](2026-07_mi-path-catalog.md) — all shipped (screen-effects health wired; atmos wiring deferred; UiShell still deferred); player HUD overlay is a partial in-branch slice (no dated effort yet) | [inventory](systems/inventory.md) — partial (player HUD); also [machine-interface](systems/machine-interface.md), [screen-effects](systems/screen-effects.md) |
| comms | [comms.md](../design/comms.md) — active | none yet (local-speech slice shipped in-branch; non-diegetic feed / PDA log still open) | [chat-audio-screens](systems/chat-audio-screens.md) — partial |
| area | [area.md](../design/area.md) — active | [area-foundation](2026-07_area-foundation.md) — shipped (partial: APC-seeded variant; live mutation recompute and editor merge/split deferred) | [area](systems/area.md) — partial |
| hacking-interface | [hacking-interface.md](../design/hacking-interface.md) — active | none yet | none yet |
| combat | [combat.md](../design/combat.md) — active | [player-body-animation](2026-07_player-body-animation.md) — shipped (stance/locomotion); [animation-polish](2026-07_animation-polish.md) — shipped (melee torso, limp, mirror, swing variants); [combat_implementation_plan](../plans/combat_implementation_plan.md) — Phase 0–1 shipped (unified melee); Phases 2–7 pending | [combat](systems/combat.md) — partial |
| stamina | [stamina.md](../design/stamina.md) — active | [2026-07_inventory-storage-redesign](2026-07_inventory-storage-redesign.md) — Phase 7a core shipped with inventory clean-slate (combat drains deferred) | [stamina](systems/stamina.md) — partial |
| health | [health.md](../design/health.md) — active | rewrite in flight: [health_implementation_plan](../plans/health_implementation_plan.md); [body-presentation-authority](2026-07_body-presentation-authority.md) — planned; screen overlays in [screen-space-effects](2026-07_screen-space-effects.md) (health wired); Main HUD alert stack health-wired (vitals UITK still open) | [health](systems/health.md) — partial |
| armor | [armor.md](../design/armor.md) — active | none yet | none yet |
| inventory-storage | [inventory-storage.md](../design/inventory-storage.md) — active | [2026-07_inventory-storage-redesign](2026-07_inventory-storage-redesign.md) — in-progress (clean-slate: data model + panel + Main HUD equip/drag + stamina 7a + old UI purge shipped; Play Mode verification pending) | [inventory](systems/inventory.md) — partial |
| examine | [examine.md](../design/examine.md) — active | none yet | [examine](systems/examine.md) — partial (character-examine target type §7 unimplemented) |
| crafting | [crafting.md](../design/crafting.md) — active | none yet | [crafting](systems/crafting.md) — stub (code obsolete / due for removal; do not extend) |
| death-cloning-respawn | [death-cloning-respawn.md](../design/death-cloning-respawn.md) — active | none yet | none yet |
| surgery | [surgery.md](../design/surgery.md) — active | none yet | none yet |
| lobby | [lobby.md](../design/lobby.md) — active | none yet | [rounds-lobby](systems/rounds-lobby.md) — shipped |
| round-config | [round-config.md](../design/round-config.md) — active | none yet | [rounds-lobby](systems/rounds-lobby.md) — shipped |
| round-end | [round-end.md](../design/round-end.md) — active | none yet | none yet |
| observer | [observer.md](../design/observer.md) — active | none yet | none yet |
| electricity | [electricity.md](../design/electricity.md) — active | none yet (electricity system is touched by [mi-area-electricity-debt](2026-07_mi-area-electricity-debt.md), but that effort implements area.md/main-hud.md, not electricity.md) | [electricity](systems/electricity.md) — partial |
| pda | [pda.md](../design/pda.md) — active | none yet | [inventory](systems/inventory.md) — partial |
| cargo | [cargo.md](../design/cargo.md) — active | none yet | none yet |
| disposal | [disposal.md](../design/disposal.md) — active | [disposal-item-network](2026-07_disposal-item-network.md) — shipped (item network; pipe craft, Cargo, player transit deferred) | [disposal](systems/disposal.md) — partial |
| id-access | [id-access.md](../design/id-access.md) — active | none yet | [id-access](systems/id-access.md) — partial |
| virology | [virology.md](../design/virology.md) — active | none yet | none yet |
| atmospherics | [atmospherics.md](../design/atmospherics.md) — active | [atmos-ecs-foundation](2026-07_atmos-ecs-foundation.md) — shipped (partial); [atmos-client-visualization-sync](2026-07_atmos-client-visualization-sync.md) — planned | [atmospherics](systems/atmospherics.md) — partial |
| chemistry | [chemistry.md](../design/chemistry.md) — active | none yet | [substances](systems/substances.md) — partial |
| explosives-destruction | [explosives-destruction.md](../design/explosives-destruction.md) — active | [structural-destruction](2026-07_structural-destruction.md) — in-progress (Phase 1–4 + blast detonation VFX) | [structural-destruction](systems/structural-destruction.md) — partial |
| construction | [construction.md](../design/construction.md) — active | none yet | [tile](systems/tile.md) — partial (staged build ladder §1-2 unimplemented; single-step placement only) |
| creative-mode | [creative-mode.md](../design/creative-mode.md) — active | none yet | none yet |
| rendering-lighting | [rendering-lighting.md](../design/rendering-lighting.md) — active | look pass planned: [urp_lighting_look_plan](../plans/urp_lighting_look_plan_d42c32f5.plan.md); polish handoff [2026-07_urp-lighting-look-polish](2026-07_urp-lighting-look-polish.md) (planned); palette emission sample fix shipped on Simple Toon | [rendering](systems/rendering.md) — partial |
| shuttles | [shuttles.md](../design/shuttles.md) — active | none yet | none yet |
| ai-cyborgs | [ai-cyborgs.md](../design/ai-cyborgs.md) — active | none yet | none yet |
| objectives | [objectives.md](../design/objectives.md) — active | none yet | none yet |
| persistence-save | [persistence-save.md](../design/persistence-save.md) — active | [persistence_architecture_design_2fe61864.plan.md](../plans/persistence_architecture_design_2fe61864.plan.md) — Phase 1a/1b shipped, Phase 2 round snapshots pending | [persistence](systems/persistence.md) — partial (station templates, server meta only) |
| networking | [networking.md](../design/networking.md) — active | [headless-dedicated-server](2026-07_headless-dedicated-server.md) — shipped (partial: selection outline and drop interaction against a real client still broken, not root-caused), [multiplayer-test-harness](2026-07_multiplayer-test-harness.md) — shipped (partial: mouse/screen-space interaction and pocket/container regressions not covered), [ci-develop-release-pipeline](2026-07_ci-develop-release-pipeline.md) — shipped (manual Windows+bats prerelease by default; Linux/EditMode/smoke opt-in) | [networking-session](systems/networking-session.md) — partial |
| audio | [audio.md](../design/audio.md) — active | none yet | [chat-audio-screens](systems/chat-audio-screens.md) — partial |
| onboarding-tutorial | none yet | none yet | none yet |
| antagonist-content | [antagonist-content.md](../design/antagonist-content.md) — active | none yet | [gamemodes-roles-traits](systems/gamemodes-roles-traits.md) — stub |
| rd-material-economy | none yet | none yet | none yet |
| cryogenics | [cryogenics.md](../design/cryogenics.md) — active | none yet | none yet |
| admin-tools | [admin-tools.md](../design/admin-tools.md) — active | none yet | [ingame-console](systems/ingame-console.md) — partial (dev/admin console, not a design spec) |
| player-accounts | [player-accounts.md](../design/player-accounts.md) — active | none yet | none yet |

[2026-07_interaction-system-hardening](2026-07_interaction-system-hardening.md) (shipped) and the infrastructure systems below (core
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
| Core / SubSystems | [core-subsystems](systems/core-subsystems.md) | shipped | `SubSystem` / `NetworkSubSystem` base types and `SubSystems` service locator; scene registration legacy — target code bootstrap |
| Application | [application](systems/application.md) | stub | App bootstrap and startup; Boot/Game as thin launch pads |
| Networking (session) | [networking-session](systems/networking-session.md) | partial | FishNet host/join session management; headless dedicated-server build; real multi-process test harness |
| Scene management | [scene-management](systems/scene-management.md) | stub | Scene loading and switching; not a system composition root |
| UI shell | [ui-shell](systems/ui-shell.md) | stub | Target UITK composition root; MI + Main HUD path catalogs shipped (duplicated); shared catalog helper + full shell deferred |
| Interactions (framework) | [interactions-framework](systems/interactions-framework.md) | shipped | Shared `IInteraction` contracts, pipeline, wire identifiers; see map § Architecture smells |
| Data / codegen | [data-codegen](systems/data-codegen.md) | stub | Asset databases and generated references; one-off Editor rebuild menus are tracked debt |
| Persistence | [persistence](systems/persistence.md) | partial | Contributor-based station templates and server meta (permissions, round history) |
| Localization | [localization](systems/localization.md) | partial | `LocalizedTextService` and examine string tables |
| Logging | [logging](systems/logging.md) | shipped | Serilog structured logging |
| Permissions | [permissions](systems/permissions.md) | partial | Admin permission checks; persisted via [persistence](systems/persistence.md) envelope with legacy txt fallback |
| Rendering | [rendering](systems/rendering.md) | partial | URP features: selection pick pass (+ exclude layers), atmospherics scatter/glow/distortion; Simple Toon palette emission; client FOV hard mask (`VisionRendererFeature` + batched raycast `_VisionMap`) |

## Gameplay

| System | Map | Status | Summary |
|--------|-----|--------|---------|
| Interactions (runtime) | [interactions-runtime](systems/interactions-runtime.md) | shipped | `InteractionController`, radial menu, armed interactions, outlines; Harm melee swing + intent↔stance; `C` double-bound with Cancel |
| Selection | [selection](systems/selection.md) | shipped | Shader-ID mesh picking; outline shells excluded from pick pass |
| Examine | [examine](systems/examine.md) | partial | Hover/detailed examine; uGUI views condemned pending UITK redesign; character-examine target type unbuilt |
| Tile / construction | [tile](systems/tile.md) | partial | Tilemap, adjacency, single-step construction placement; TileMap Creator uGUI condemned; `TileCoord` must be `IEquatable` for dict keys; staged build ladder (construction.md §1-2) unbuilt |
| Atmospherics | [atmospherics](systems/atmospherics.md) | partial | ECS turf gas sim; GPU fog/fire on server/host only — client VFX sync planned; tick GC pitfalls documented (upload/pipes) |
| Area | [area](systems/area.md) | partial | APC-seeded flood-fill, area power, lighting state, wall light switches |
| Electricity | [electricity](systems/electricity.md) | partial | kWh storage, HV cable grid, APC/SMES/generators, consumer visuals |
| Substances | [substances](systems/substances.md) | partial | Containers, transfer interactions, Tier 2 armed proof-of-concept; container `AsReadOnly` GC pitfall |
| Inventory | [inventory](systems/inventory.md) | partial | Items/containers/hands + weight/size-class/stacking/locks; Main HUD sole equip/storage UI + StoragePanel + zone reticle (lock-on recharge + connect flash) + intent chip (polls `CurrentIntent`); HUD suppressed while MI open; old uGUI purged; `CarriedWeight` → stamina; Human hands wiring remains prefab debt |
| Stamina | [stamina](systems/stamina.md) | partial | Phase 7a core: health-modulated regen, encumbrance, sprint drain, overdraw→oxy; no permanent bar; combat drains deferred |
| Entities | [entities](systems/entities.md) | partial | Humanoids, minds, spawning; body-state animation + combat stances + injured limp/severity/mirror; Harm intent → combat stance; shelved Misc/Probably Not clips; `Human.prefab` composition debt; collapse/death presentation debt ([body-presentation-authority](2026-07_body-presentation-authority.md)) |
| Health | [health](systems/health.md) | partial | Phases 1–5b shipped; screen-effects wired from snapshot; Phase 0d strips/rewires `Human.prefab`; vitals HUD / examine-self Phase 6 remainder; interim collapse RPCs — see [body-presentation-authority](2026-07_body-presentation-authority.md); limp/`InjuredLeg`/arm injury → body anim ([animation-polish](2026-07_animation-polish.md)); zone resolve exclude-self + AnatomyNode for melee |
| Combat | [combat](systems/combat.md) | partial | Phase 0–1 melee: Harm click always swings, camera-ray connect (exclude self); `C`/chip toggles intent→stance; reticle lock-on + cross flash; disarm/ranged/armor deferred |
| Crafting | [crafting](systems/crafting.md) | stub | Obsolete / due for removal; menu uGUI condemned; `Craft` on hands is outline landmine until purge |
| Furniture / world objects | [furniture](systems/furniture.md) | partial | Airlocks, lockers, vendors, jukebox; disposal chutes/outlets delegate to [disposal](systems/disposal.md); vending via diegetic machine-interface |
| Structural destruction | [structural-destruction](systems/structural-destruction.md) | partial | Phase 1–4: Turf integrity stages; melee StructuralForce; blast BFS + cascade; Destroyed→clear; Cracked airtightness; Area deferred live reflood; MPB stage tint + Cracked hiss; examine; epicenter blast VFX (fireball/light/scorch/shake/flash); `hurtstructure` / `blast` |
| Disposal | [disposal](systems/disposal.md) | partial | Item network: BFS pipes, chute SizeClass gate, capsules, outlet grace/despawn; pipe craft, Cargo, player transit deferred |
| Rounds / lobby | [rounds-lobby](systems/rounds-lobby.md) | shipped | Round state machine; lobby UI condemned pending lobby.md redesign |
| Gamemodes / roles / traits | [gamemodes-roles-traits](systems/gamemodes-roles-traits.md) | stub | Objectives, job roles, character traits |
| Player control | [player-control](systems/player-control.md) | stub | Player subsystem and input routing |
| Chat / audio / screens | [chat-audio-screens](systems/chat-audio-screens.md) | partial | Local speech chips + T-compose; always-on chat UI Phase 0 purged (headless ChatSubSystem); audio/camera; camera ownership planned ([camera-ownership](2026-07_camera-ownership.md)) |
| Machine interface UI | [machine-interface](systems/machine-interface.md) | shipped | Diegetic APC/SMES/atmos/vending; path catalog; Dual Kawase blur + dim; DOTween bring-up/dismiss |
| Screen-space effects | [screen-effects](systems/screen-effects.md) | partial | URP Volume overlays; health drives dying/blood/oxy/concussion/unconscious + hit flash; `SetUiBackdropBlur` for machine UI; atmos temp/fire deferred; F2 debug Canvas condemned |
| ID / access | [id-access](systems/id-access.md) | partial | Crew records, credential checks, doors, machine UI gates, dev console helpers |
| Inputs | [inputs](systems/inputs.md) | partial | Arbitration + `InputInterface` UITK/uGUI pointer authority (Main HUD / MI / radial register documents) |
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
| [2026-07_area-foundation](2026-07_area-foundation.md) | shipped (deferred: live mutation recompute, editor merge/split) |
| [2026-07_atmos-ecs-foundation](2026-07_atmos-ecs-foundation.md) | shipped (deferred: liquid/solid phase, pipes, pumps, client VFX sync) |
| [2026-07_map-editor-replacement](2026-07_map-editor-replacement.md) | shipped |
| [2026-07_tile-overlay-replacement](2026-07_tile-overlay-replacement.md) | shipped |
| [2026-07_atmos-client-visualization-sync](2026-07_atmos-client-visualization-sync.md) | planned |
| [2026-07_mi-area-electricity-debt](2026-07_mi-area-electricity-debt.md) | shipped |
| [2026-07_player-body-animation](2026-07_player-body-animation.md) | shipped (foundation; polish in [animation-polish](2026-07_animation-polish.md)) |
| [2026-07_animation-polish](2026-07_animation-polish.md) | shipped (melee torso, limp severity/oneshots, left-hand mirror, swing variants) |
| [2026-07_screen-space-effects](2026-07_screen-space-effects.md) | shipped (foundation + health wiring; atmos deferred) |
| [2026-07_headless-dedicated-server](2026-07_headless-dedicated-server.md) | shipped (partial: selection outline, drop interaction against a real client still broken, not root-caused) |
| [2026-07_agent-first-composition](2026-07_agent-first-composition.md) | shipped (policy); code deferred — bootstrap, UiShell + shared path-catalog helper, prefab tooling; main-HUD UITK slice partial ([inventory](systems/inventory.md)) |
| [2026-07_mi-path-catalog](2026-07_mi-path-catalog.md) | shipped (MI path catalog wedge of composition follow-on b; Main HUD later copied the pattern — unify under [ui-shell](systems/ui-shell.md)) |
| [2026-07_body-presentation-authority](2026-07_body-presentation-authority.md) | planned |
| [2026-07_inventory-storage-redesign](2026-07_inventory-storage-redesign.md) | in-progress (clean-slate + stamina 7a code shipped; Play Mode verification pending) |
| [2026-07_multiplayer-test-harness](2026-07_multiplayer-test-harness.md) | shipped (partial: mouse/screen-space interaction and pocket/container round-trip regressions not covered; not yet verified against a real Unity build) |
| [2026-07_ci-develop-release-pipeline](2026-07_ci-develop-release-pipeline.md) | shipped (manual workflow_dispatch; default Windows+bats prerelease; Linux/EditMode/smoke opt-in) |
| [2026-07_disposal-item-network](2026-07_disposal-item-network.md) | shipped (item network; pipe craft, Cargo, player transit deferred) |
| [2026-07_camera-ownership](2026-07_camera-ownership.md) | planned (dedicated camera manager / contexts; same ownership smell as pre-arbiter input) |
| [2026-07_input-arbitration](2026-07_input-arbitration.md) | shipped |
| [2026-07_unity-perf-ai-tooling](2026-07_unity-perf-ai-tooling.md) | shipped (Editor Profiler → Logs/perf markdown + analyze-unity-perf skill; player capture / console / budgets deferred) |
| [2026-07_structural-destruction](2026-07_structural-destruction.md) | in-progress (Phase 1–4 + blast detonation VFX) |

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
| [animation_system_design_250de599.plan.md](../plans/animation_system_design_250de599.plan.md) | Player body / layered animation foundation (+ polish notes) |
| [health_implementation_plan.md](../plans/health_implementation_plan.md) | Clean-slate health rewrite (Phases 0–5b shipped; 6–9 pending) |
| [combat_implementation_plan.md](../plans/combat_implementation_plan.md) | Clean-slate combat: Phase 0–1 unified melee shipped (camera-ray connect, intent↔stance, reticle 2A); disarm/ranged/stamina/armor later |
| [urp_lighting_look_plan_d42c32f5.plan.md](../plans/urp_lighting_look_plan_d42c32f5.plan.md) | URP half-toon look pass (pending) |
| [station_structural_damage.plan.md](../plans/station_structural_damage.plan.md) | Structural integrity / blast / items (Phase 1–4 shipped; 5–6 pending) |
| [shuttle_system_design_a3dd2e04.plan.md](../plans/shuttle_system_design_a3dd2e04.plan.md) | Shuttle tile blueprints / multi-map (pending) |

## Design specs (read-only)

Gameplay specs in [Documents/design/](../design/) — owner-maintained. Agents link, never edit.
See the [coverage table](#coverage-table) above for design/architecture/system-map status
per domain.

## Reference (non-system)

| Resource | Path | Use when |
|----------|------|----------|
| Technical debt tracker | [TECH_DEBT.md](TECH_DEBT.md) | Prioritizing structural risk / code smells before picking up cleanup work |
| Art asset index | [art-asset-index.md](../art-asset-index.md) | Locating or importing art from SS3D-Art |
| Available for import | [art-available-for-import.json](../art-available-for-import.json) | Finding game-ready art not yet in `Assets/Art/` |
| UI icon index | [icon-index.md](../icon-index.md) | Finding external game-icons SVGs for UI work |
