# Fork status

This fork ([henkhooft/SS3D](https://github.com/henkhooft/SS3D)) has diverged meaningfully from
[RE-SS3D/SS3D](https://github.com/RE-SS3D/SS3D) upstream. It is an active experiment in
redesigning core gameplay systems and development foundations at a faster pace than upstream's
current review capacity supports.

This document is the plain-language divergence log. It is updated periodically — not per-commit.
For doc authoring conventions see [SKILL.md](SKILL.md).

**Last updated:** 2026-07-21

---

## At a glance

| | Upstream (`RE-SS3D/SS3D`) | This fork (`henkhooft/SS3D`) |
|---|---|---|
| Default branch | `develop` | `develop` |
| Unity version | 2021.3.15f1 | **6000.3.16f1** (Unity 6) |
| Render pipeline | Built-in | **URP 17** |
| Release channel | Tagged releases on GitHub | **Manual prereleases** via `develop-release.yml` (Windows zip + bats by default; Linux / EditMode / smoke opt-in) — day-to-day still build-from-source |
| Documentation | GitBook ([ss3d.gitbook.io](https://ss3d.gitbook.io/dev-guide/)) | `Documents/design/` + `Documents/architecture/` + system maps |
| Commits ahead of upstream | — | **~641** (0 behind as of 2026-07-21 refetch) |
| Files changed vs upstream | — | ~13.7k files, +1.45M / −107k lines |

### Shipped gameplay

Player-facing systems on this fork that upstream does not have (or has only in a thinner form).
Engine, CI, and agent/docs tooling stay out of this table — see sections below for those.
Coverage means how much of the design intent is playable today, not “code exists.”

| Feature | Coverage | What shipped |
|---|---|---|
| [Selection + examine](#selection-api-1387) | shipped | Shader mesh picking; hover tooltips; Shift-hold detailed examine (text/image) |
| [Interactions + radial menu](#interactions-hardening-and-radial-menu) | shipped | Hardened multiplayer interactions; three-tier UITK radial; armed Tier-2 targeting |
| [Main HUD](#main-hud-ui-toolkit) | partial | Gear/hands/intent UITK overlay; real on-demand storage panels (see Inventory row); zone reticle for melee; vitals/self-examine still pending |
| [Inventory & storage](#inventory-and-storage-redesign) | partial | Clean-slate Container model: weight, size-class fit, stacking, locks; on-demand panel with drag-drop; backpack/toolbelt/locker wired; Play Mode verification pending |
| [Comms](#comms-local-speech-and-crowd-cap) | partial | Local speech chips with distance/occlusion tiers, T-compose, crowd cap; radio/channels, non-diegetic feed, PDA log still open |
| [Disposal](#disposal-item-network) | partial | BFS pipe routing, chute/outlet transit, SizeClass gate; pipe crafting, Cargo hook, player transit deferred |
| [Health](#health-rewrite) | partial | Two-tier damage, organs, bleeding, critical/defib, field treatments, limb severing; vitals HUD pending |
| [Combat](#combat-phase-0-1-clean-slate-melee) | partial | Phase 0–1 clean-slate melee: Harm always swings, camera-ray connect, reticle lock-on/flash, intent↔stance; disarm/ranged/armor/blocking not built |
| [Body animation](#player-body-animation-foundation) | partial | Peaceful / melee / ranged stance + aim IK; injured limp gait, left-hand mirror, swing variants (animation polish) |
| [Screen effects](#screen-space-effects) | partial | Dying/blood/oxy/concussion/unconscious + hit flash from health; atmos temp/fire not wired |
| [Vision / FOV](#vision-fov) | shipped | Hard black fog-of-war from viewpoint raycasts |
| [Station lighting (URP)](#urp-lighting-phase-1) | partial | Fixture-driven half-toon Forward+ look (zero ambient, dual fixture lights, emergency dim); visual plate polish still open |
| [Areas + station power](#area-foundation) | partial | APC-seeded areas, kWh cells, channel shedding, area lights/switches; live remesh pending |
| [Machine interfaces](#machine-interfaces) | shipped | Diegetic UITK APC/SMES/atmos ports/vending; engineering ID swipe on gated panels |
| [Atmospherics](#atmospherics-ecs-foundation) | partial | Turf gas ECS + fire; pipe networks, vents/scrubbers/pumps/air alarms; client VFX sync pending |
| [ID / access](#id-access-foundation) | partial | Crew records, door + machine gates, ID console; auth logs / broader design pending |
| [Tilemap / construction](#tilemap-and-adjacency-engine) | shipped | Adjacency engine, construction service, AOI tile sync, layer visibility |
| [Map Editor](#map-editor-replacement) | partial | Full-screen UITK Map Editor replaces TileMap Creator; creative-mode Builder / instant place / round-config pool deferred |
| [Rounds](#game-lifecycle-hardening) | shipped | Single-flight round state machine (join/start/end races hardened) |

Details and deferred work for each row live under [Shipped on `develop`](#shipped-on-develop).

---

## Repository and documentation

### Presentation

- [README.md](../README.md) and [CONTRIBUTING.md](CONTRIBUTING.md) describe this fork, not upstream.
- Issue templates and the PR checklist point contributors at `Documents/` rather than GitBook.
- [ISSUE_TEMPLATE/Config.yml](../.github/ISSUE_TEMPLATE/Config.yml) links discussions and this file
  instead of upstream milestones.

### Documentation structure

Independent of upstream's GitBook:

| Path | Purpose |
|---|---|
| [SKILL.md](SKILL.md) | Conventions for all documentation layers |
| [architecture/INDEX.md](architecture/INDEX.md) | Navigation hub — find code by system; project-wide coverage table |
| [architecture/systems/](architecture/systems/) | Per-domain system maps (entry points, key files) |
| [design/](design/) | Gameplay design specs — **what** and **why** (owner-maintained) |
| [architecture/](architecture/) | Dated implementation efforts — **how** and **in what order** |
| [plans/](plans/) | Temporary implementation plans (updated when work ships) |
| [AGENTS.md](../AGENTS.md) | AI agent instructions (docs-first navigation) |
| [CLAUDE.md](../CLAUDE.md) | Condensed repo/navigation guidance for Claude Code |
| [FORK_STATUS.md](FORK_STATUS.md) | This divergence log |

Upstream's dev guide may still help with generic Unity and FishNet concepts, but project direction,
milestones, and gameplay specs for this fork live in `Documents/`.

### GitHub automation

Upstream-only workflows (milestones, roadmap, Discord notifications, old packaging helpers)
were **removed** from this fork. Remaining workflows:

- [develop-release.yml](../.github/workflows/develop-release.yml) — **manual** (`workflow_dispatch`)
  Windows client → GitHub **prerelease** (zip + launch bats) by default; Linux client/server,
  EditMode, and multiplayer smoke are opt-in inputs — see
  [2026-07_ci-develop-release-pipeline.md](architecture/2026-07_ci-develop-release-pipeline.md)
- [editmodetestrunner.yml](../.github/workflows/editmodetestrunner.yml) — EditMode tests on push to
  `develop`, PRs (`pull_request_target`), and manual dispatch; forces Node 24 for JS actions;
  validates `UNITY_LICENSE` / `UNITY_SERIAL` secrets before running
- [multiplayer-smoke-test.yml](../.github/workflows/multiplayer-smoke-test.yml) — opt-in headless
  multiplayer smoke (`basic-round` / `late-join`) against a real dedicated-server + client build

Doc layer redesign (coverage table, strip prototyping prompts from design docs) merged via PR #2.
Agent-first composition policy and UI path-catalog conventions live under
[architecture/](architecture/) (see shipped sections below). A design/implementation alignment
audit (PR #18) fixed code that had drifted from its design doc. Design-docs integration (PR #21)
added antagonist / audio / networking / player-accounts specs. Cross-cutting structural debt lives
in [TECH_DEBT.md](architecture/TECH_DEBT.md) (PR #25).

---

## Engine and toolchain

These changes touch nearly every asset and are the largest structural divergence from upstream.

### Unity 6 upgrade

Merged via `archive/develop-unity6`. Includes:

- Editor upgrade to **6000.3.16f1**
- Asset and ProjectSettings reserialization to Unity 6 format
- ugui 2.0 / TMP Essential Resources import
- Addressables 2.x cleanup (legacy Built In Data entries removed)
- Scriptable Build Pipeline settings
- Input System Controls.cs regeneration (inputsystem 1.19.0)
- FishNet DefaultPrefabObjects registry rebuild
- Editor toolbar tools migrated from UnityToolbarExtender to Unity 6 **MainToolbar API**
  (Scene Switcher, Launcher, Network Settings — see README)

### URP migration

Merged via `archive/feature-urp-migration` (through `develop-unity6`). Includes:

- URP 17 foundation (`com.unity.render-pipelines.universal` 17.3.0)
- Pipeline asset and Forward+ renderer in `Assets/Settings/URP/`
- Simple Toon shader port to URP HLSL
- Game content materials converted from Built-in Standard to URP Lit
- Post Processing Stack v2 removed; post-processing migrated to URP volumes
- Pipeline MSAA disabled so camera TAA can run without warnings
- Editor migration tooling in `Assets/Editor/URPMigration/`
- **Palette emission fix** (PR #7) — Simple Toon samples `_EmissionMap` so shared
  `PaletteEmission` materials get UV swatch colors instead of a flat white `_EmissionColor`

Upstream remains on the Built-in render pipeline with no equivalent URP assets or selection-pick
render features.

### URP lighting Phase 1

**Paths:** `Assets/Content/Resources/Simple Toon/`, fixture / `LightPower` behaviours,
`Assets/Settings/URP/`

Fixture-driven half-toon station lighting on URP 17 Forward+ (merged via PR #24,
`feature/urp-lighting-phase1-v2`):

- Zero ambient / no main directional light; gameplay Volume + SSAO on the Forward+ renderer
- Half-toon lighting in `STLighting.hlsl` with soft-near attenuation for fill hotspots
- Dual fixture lights (Spot + PointFill) on tubes/bulbs; emergency mode is short-range spot-only
  (fill off) so unpowered rooms read as power-starved
- Floor derivative normals; inventory icon previews get temporary lights so icons stay readable
  under fixture-only scene lighting

Visual plate matching upstream Built-in reference screenshots is **not** finished — handoff in
[2026-07_urp-lighting-look-polish.md](architecture/2026-07_urp-lighting-look-polish.md).
Plan: [urp_lighting_look_plan_d42c32f5.plan.md](plans/urp_lighting_look_plan_d42c32f5.plan.md).
System map: [rendering.md](architecture/systems/rendering.md).

### Analyzer tooling

StyleCop analyzers disabled in the project (2026-07-14) to reduce friction on AI-assisted edits.
EditMode test CI still runs on push and PR.

### Technical debt tracker

**Path:** [TECH_DEBT.md](architecture/TECH_DEBT.md)

Project-wide register of structural risks (mega-prefab, collapse authority, interaction Discover
contract, condemned crafting, etc.) ranked by blast radius — not a fourth doc layer; indexes
system-map Pitfalls and agent-first composition debt. Merged via PR #25.

---

## Shipped on `develop`

These systems are implemented and merged. They represent the bulk of code divergence from upstream.

### Selection API (#1387)

**Paths:** `SS3D.Systems.Selection`, `SS3D.Rendering.URP.SelectionPick*`

Shader-ID mesh picking replaces naive screen raycasts for interaction targeting. Each `Selectable`
gets a unique render color; a URP offscreen pick pass plus `SelectionCamera` readback identifies
the hover target. `InteractionController` routes client interaction targeting through this system;
the server validates using `NetworkObject` and interaction point.

Merged from `archive/feature-1387-selection-api`. Cursor picking misalignment fix merged from
`archive/fix-selection-camera-picking`.

**Post-merge polish** (PR #5): hover flicker and outline bleed into item icons fixed by excluding
outline shells from the pick pass via `SelectionRenderingLayers` (lives in `Rendering.URP` to avoid
assembly cycles); green outline cleared on item pickup.

**Post-merge polish** (PR #17): `ClosestPoint` no longer called on non-convex `MeshCollider`s
(was throwing/misbehaving on some selectable meshes).

### Detailed examine (#1394)

**Paths:** `SS3D.Systems.Examine`

Extends hover tooltips with **shift-hold detailed examine** — text and image panel variants,
range-gated, driven off the selection system's current `IExaminable`. Posters and other content
wired as `ImageExaminable`.

Merged from `archive/feature-1394-detailed-examine`.

### Examine localization

**Paths:** `SS3D.Localization`, `SS3D.Systems.Examine`

Unified **Examine** string table for all 147 `ExamineData` assets, replacing sparse per-category
tables (Items, Tiles, Misc). Shared **`LocalizedTextService`** (caching, locale-change invalidation,
dev/release fallback) drives code-driven lookups; **`ExamineContentResolver`** separates static
table text from dynamic sections via **`IExamineContentProvider`**.

English strings migrated and wired through `LocalizedString` fields on every examine asset. Editor
menus under `SS3D/Localization/Examine/` export/import JSON for translation workflows.
**Identification cards** use dynamic owner/role lines (`IdentificationCardExaminable`).

Non-English locales (`fr`, `pt-BR`, `ru-RU`) have empty Examine tables for now — they fall back to
English until a translation import lands.

Merged from `archive/feature-examine-localization`. Design plan:
[examine_localization_design_5ca361a6.plan.md](plans/examine_localization_design_5ca361a6.plan.md).

**Examine UX:** hover + Shift-hold detailed panels via `ExamineSubSystem` / `ExamineUI` off the
current selection — **not** an `IInteraction` petal. A radial Tier 1 Examine petal was planned but
is not shipped as `ExamineInteraction` on `develop`.

### Interactions — hardening and radial menu

**Paths:** `SS3D.Interactions`, `SS3D.Systems.Interactions`

Production multiplayer hardening of the source/target interaction model:

- **`InteractionIdentifier`** wire protocol (`genericName` + `targetComponentIndex`) — RPCs no longer
  match display names
- **`InteractionPipeline`** — shared discover → filter → sort on client and server; **`Priority`**
  for deterministic primary-click order
- Gameplay gates: intent sync, stamina, inventory ownership, locker **`InteractionPermission`**
- Cancellation (**C** key + movement auto-cancel on delayed interactions)
- Optimistic client feedback (loading bars + pending hover outlines); **`TargetRejectInteraction`**
  rollback on server reject
- Hover availability outlines (green/yellow/blue) via **`InteractionOutlineView`** + URP outline shader

**Three-tier radial menu** (UI Toolkit, shared tokens at `Assets/Content/Systems/UI/Tokens/`):

- Dynamic petals with labels; instant (Tier 1), armed targeted (Tier 2), combine (Tier 3 — drag route
  still pending)
- **`ArmedInteractionSubSystem`** + overlay for second-click targeting (`TransferSubstanceInteraction`
  proof-of-concept)
- Radial HUD lives on scene overlay (`Game.unity`), not `PlayerCanvas`

EditMode **`InteractionPipelineTests`**; PlayMode pickup regression via **`InteractionPlayModeTests`**.

Merged from `archive/feature-interaction-system-hardening` (includes `archive/feature-radial-menu-redesign`).
Architecture: [2026-07_interaction-system-hardening.md](architecture/2026-07_interaction-system-hardening.md).
Plans: [interaction_system_improvements_9e14ae22.plan.md](plans/interaction_system_improvements_9e14ae22.plan.md),
[radial_menu_implementation_5a83bdf9.plan.md](plans/radial_menu_implementation_5a83bdf9.plan.md).

**Post-merge polish** (2026-07-12): stuck outline cleanup, drop-on-click duplicate-spawn fix, instant-interaction
loading-bar flash, toggle-interaction Power icon fallback, radial menu polish, crafting outline log spam.
Removed FastScriptReload and repaired Game scene missing scripts.

Merged from `archive/feature-interactions`.

**Post-merge polish** (PR #17): outline now shows only for objects with a target-bound interaction;
range enforced when an interaction point is missing; Craft discovery gated behind a check, with the
underlying interaction-architecture smells documented rather than silently worked around.

### Tilemap and adjacency engine

**Paths:** `SS3D.Systems.Tile`, `SS3D.Systems.Tile.Connections`

Major refactor of the construction tilemap:

- **AdjacencyEngine** — queued adjacency recompute replacing recursive neighbour ping-pong;
  connectors migrated for walls, doors, pipes, cables, disposal pipes, furniture, and more
- **TileAdjacencyView** — local mesh/direction visuals synced from adjacency state
- **ITileQueryService** / **TileQueryService** — read-only tile queries
- **ConstructionService** — server-authoritative placement (Phase 3)
- **Tile identity sync** — compact ushort asset catalog (Phase 1)
- **FishNet HashGrid AOI** — tile replication scoped by area-of-interest; fixes for tile pop-in
  when re-entering AOI
- TileMap Creator RPCs gated behind server-side Administrator checks
- **Build-menu layer visibility** — client-only dim/restore of non-selected tile layer groups in the
  construction build tab (`TileLayerVisibilityService`); no network sync

Merged from `archive/feature-tilemap-system-refactor` and
`archive/tilemap-layer-visibility` (build-menu layer toggles).

The in-game **Map Editor** (below) replaces the legacy TileMap Creator uGUI for admin authoring;
layer visibility is shared with the editor toolbar.

### Map Editor replacement

**Paths:** `SS3D.Systems.Tile.MapEditor`, `Assets/Content/Systems/UI/MapEditor/`

Full-screen **UI Toolkit** Map Editor replacing the DynamicPanels TileMap Creator tab
(merged via PR #26, `claude/map-editor-creative-mode-8pp6qj`):

- Tools: Select / Edit / Move / Delete / Dropper; undoable compound place/delete/decal commands
- Admin-gated via `IMapEditorAuthorizer` (default `AdminMapEditorAuthorizer`); catalog regenerated
  from Editor menu
- Layer visibility, overlays subcategory (sparse floor decals), save/load local station templates
- Orphan TileMap Creator UI prefabs purged so AssetAudit no longer sees missing scripts
- Creative-mode extension hooks reserved (`MapEditorPlacementMode`, round-config save target,
  Area tool stub) — see [map-editor-creative-hooks.md](architecture/systems/map-editor-creative-hooks.md)

Deferred: creative-mode Builder role / instant placement, area merge/split tool, round-config map
pool save, Scripting & Placements backends.

Architecture: [2026-07_map-editor-replacement.md](architecture/2026-07_map-editor-replacement.md).
System map: [tile.md](architecture/systems/tile.md). Design:
[creative-mode.md](design/creative-mode.md).

### Game lifecycle hardening

**Paths:** `SS3D.Systems.Rounds`

Round loop refactored to a **single-flight state machine** with generation-tracked
`CancellationTokenSource` (prevents double start/stop and embark-during-ending races). States:
`Stopped → Preparing → WarmingUp → Ongoing → Ending → Ended`. Join and round action ordering
hardened across `EntitySubSystem`, `PlayerSubSystem`, and `GamemodeSubSystem`.

Regression tests added under `Assets/Scripts/Tests/KnownIssueReproduction/RoundLifecycle_*`.

Merged from `archive/feature-game-lifecycle-hardening`.

### Machine interfaces

**Paths:** `SS3D.UI.MachineInterface`, `Assets/Content/Systems/UI/MachineInterface/`

Diegetic **UI Toolkit** panels for station machines, networked via FishNet snapshots:

| Phase | Status | What shipped |
|---|---|---|
| [Phase 1](architecture/2026-07_machine-interface-phase1-foundation.md) | Shipped | UI foundation, local APC panel preview |
| [Phase 2](architecture/2026-07_machine-interface-phase2-apc-networking.md) | Shipped | Networked APC with power channel gating |
| [Phase 3](architecture/2026-07_machine-interface-phase3-smes-generalization.md) | Shipped | SMES interface (exterior + engineer views), registry-driven plumbing, shared binder flow |
| [Diegetic screen UI](architecture/2026-07_diegetic-screen-ui-framework.md) | Shipped | Reusable diegetic device shell + component library; vending machine as first consumer |

**APC and SMES** now use `DiegeticDeviceShell` (chassis/bezel/screen) with engineering
`AccessGatePanel` ID swipe gates, glanceable status chips, channel rows, and steel-button controls.
Legacy modal `MachineWindow` remains available for simple panels but is no longer used by APC/SMES.

**Atmospheric devices** ship diegetic panels with the same shell pattern:

- **Air alarm** — live turf sampling (tile in front of wall mount), area vent/scrubber discovery,
  preset mode dispatch, plasma/temperature readouts, gas bar widgets
- **Scrubber** — per-gas filter toggles and flow-rate stepper wired into `ScrubberController`
- **Vent** — target pressure control enforced in `VentController` (stops filling at target)
- **Pump** — inlet/outlet pressure readouts, target outlet pressure, power toggle; operates on its
  own tile and pipe network only (not area-linked)

Power toggles on atmos port UIs require engineering ID access. Diegetic panels support vertical
scrolling for tall content. `MachineInterfaceShellKind` registers modal vs diegetic layouts on the
host.

**Vending machines** open a networked diegetic panel (`VendingMachineController`) with tray-based
dispense (vend to tray, then take). Legacy per-product `DispenseProductInteraction` and
`VendingMachine` behaviour removed. Vending stays **ungated** (no engineering ID swipe) even after
the ID/access foundation shipped for APC/SMES/atmos panels.

**Post-merge polish** (PR #4): refresh-driven UI teardown no longer drops Take/vend click handlers;
`MachinePowerConsumer` no longer sticks at “in use” wattage after vend.

**Backdrop blur** (PR #14): diegetic panels blur the world behind them via Dual Kawase blur plus a
dark dim, replacing an earlier depth-of-field approach.

`MachineInterfaceHost` disables `UIDocument` when closed to avoid interfering with the selection
pick pass. Diegetic panels mount the full cloned UXML `TemplateContainer` so attached style sheets
apply at runtime. UI Toolkit masking rule: never combine `border-radius` and `overflow: hidden` on
the same element — split painted and clipping layers (`DiegeticDeviceShell`, `PanelSection`).

Merged from `archive/machine-ui` (APC/SMES networking),
`archive/feature-diegetic-screen-ui-framework` (diegetic shell + vending), and
`atmos-pipes` (APC/SMES diegetic redesign + atmos device panels). Backdrop blur merged via PR #14
(`cursor/diegetic-ui-backdrop-blur`). Plan:
[diegetic_screen_ui_framework_643c2e6f.plan.md](plans/diegetic_screen_ui_framework_643c2e6f.plan.md).
System map: [machine-interface.md](architecture/systems/machine-interface.md).

### Area foundation

**Paths:** `SS3D.Systems.Area`, `SS3D.Systems.Electricity`

APC-seeded per-tile area partition with flood-fill, save/load, and area-scoped power and lighting:

- **`AreaSubSystem`** + **`AreaFloodFillService`** — first-wins APC order, door-tile post-pass, wall-mount
  seeding from `FacingDirection`; per-chunk `ushort[]` area ids; overlap diagnostic in APC machine UI
- **Area-scoped power** — `AreaApcPowerDistribution` draws grid headroom to each APC and drains its cell
  per area; consumers (vendors, jukebox, air alarms, airlocks) do not join the HV cable graph
- **kWh storage model** — tick-integrated charge/discharge; priority channel shedding (Equipment →
  Environment → Lighting)
- **Lighting state** — `AreaLightingState` derivation + client sync; `LightPower` fixture visuals;
  `LightSwitchController` toggles player lighting preference (distinct from APC lighting breaker)
- **Consumer visuals** — `ConsumerPowerVisual` dims emissive/panel materials; airlocks power-gated via
  `AirLockOpener` with delayed close on power loss
- **Atmos port power** — vents, scrubbers, pumps, and air alarms draw from the **Environment**
  power channel via `BasicPowerConsumer`; port simulation skips when unpowered

EditMode tests under `Assets/Scripts/Tests/EditMode/AreaTests/` and
`Assets/Scripts/Tests/EditMode/ElectricityTests/`.

Merged from `archive/areas-foundation` and extended by `atmos-pipes` (environment channel for atmos
ports). Architecture:
[2026-07_area-foundation.md](architecture/2026-07_area-foundation.md). Plans:
[areas_implementation_plan_c0639343.plan.md](plans/areas_implementation_plan_c0639343.plan.md),
[electricity_kwh_foundation_917ccdbc.plan.md](plans/electricity_kwh_foundation_917ccdbc.plan.md).

Deferred: live tile-mutation recompute; editor merge/split UI.

**Post-merge polish** (PR #17): `PowerStatus` flicker fixed so airlocks can actually close;
APC flood-fill now deferred until station template load finishes, instead of racing it.

### Atmospherics ECS foundation

**Paths:** `SS3D.Systems.Atmospherics`, `SS3D.Rendering.URP.AtmosRendererFeature`

Server-authoritative open-tile gas simulation on the turf grid:

- **ECS world** — `AtmosSimulation` with native cell buffers, active-cell sleep/wake, Burst jobs
  (`ShareGasJob`, `ConductHeatJob`, `ReactAtmosJob` for plasma combustion)
- **Tile bridge** — `AtmosTileObserver` on `ITileMutationObserver`; dynamic airlock occupancy via
  `IDynamicTileOccupant` reopens/closes gas paths when doors move
- **GPU visualization** — `AtmosGpuUploader` → pressure/temperature/composition/fire textures;
  `AtmosRendererFeature` scatter + plasma glow + heat distortion passes
- **Debug** — `AtmosDebugController` overlay; shader debug views on renderer feature

EditMode tests under `Assets/Scripts/Tests/EditMode/Atmospherics/`.

Merged from `archive/feature-atmos-ecs`. Architecture:
[2026-07_atmos-ecs-foundation.md](architecture/2026-07_atmos-ecs-foundation.md).

Deferred: liquid/solid phase buffers, valves, liquid pipe networks, pipe failures, chemistry
integration, **client VFX sync** (server/host only today — see
[2026-07_atmos-client-visualization-sync.md](architecture/2026-07_atmos-client-visualization-sync.md)).

### Atmospherics pipe machinery

**Paths:** `SS3D.Systems.Atmospherics.Pipes`, `SS3D.UI.MachineInterface` (atmos panels)

Gas pipe layer on top of the turf ECS simulation — bulk pipe networks exchange gas with turf cells
via registered port devices:

- **Pipe networks** — `GasPipeNetworkRegistry`, `AtmosPipeSimulation`, tile-driven connectivity via
  `AtmosPipeObserver`; bulk pressure/mole sharing across connected segments
- **Vents** — `VentController` pushes network gas into turf until target pressure reached
- **Scrubbers** — `ScrubberController` pulls filtered gases from turf into the network; UI flow rate
  scales rated throughput
- **Pumps** — `AtmosPumpController` moves turf gas into the pipe network with target outlet pressure
  and max differential stall; not area-linked
- **Air alarms** — `AirAlarmController` samples the turf cell in front of the wall mount, discovers
  area vents/scrubbers via `AtmosAreaDeviceQuery`, and dispatches preset modes; port commands
  validated against the alarm's resolved area
- **Power gating** — port devices require Environment channel power (`AtmosPortPower`)
- **VFX polish** — fire clipping, wall occlusion, and heat distortion placement fixes on the atmos
  render passes
- **Debug** — `AtmosDebugController` extended with pipe network overlay

EditMode tests under `Assets/Scripts/Tests/EditMode/Atmospherics/` (port flow, pump stall/target,
scrubber flow rate, air-alarm sampler). Gas pump examine strings added to the unified Examine table.

Merged from `atmos-pipes`. System map: [atmospherics.md](architecture/systems/atmospherics.md).

Deferred: valves, liquid pipes, pipe failures (clog/rupture), junction chemistry.

### ID / access foundation

**Paths:** `SS3D.Systems.IdAccess`, `SS3D.UI.MachineInterface`, `SS3D.Systems.Furniture`

Server-side crew identity rail and shared access checks:

- **`IdAccessSubSystem`** + **`CrewRecord`** — authoritative access bitmask per ckey; physical `IDCard`
  tokens bind to records (no independent card state)
- **`AccessCredentialResolver`** — finds bound ID on hands and inventory containers (direct card or
  PDA-inserted)
- **Machine UI gates** — `AccessGatedMachineInterfaceBehaviour` server ID scan sessions; granted,
  scanning, and denied states on diegetic ID reader variants
- **Door access** — `AirLockAccessGate` checks holder credential against area default or override
- **ID console** — `IdConsoleController` for Change ID gated editing; dev console commands
  (`accesscheck`, `accessgrant`, `accessrevoke`, `accesspreset`)

EditMode tests under `Assets/Scripts/Tests/EditMode/IdAccessTests/`.

Merged from `archive/feature-id-access-foundation`. System map:
[id-access.md](architecture/systems/id-access.md).

Deferred: full design-spec coverage (auth logs, comms integration, playtime/job unlocks).

### Persistence foundation

**Paths:** `SS3D.Data.Persistence`, `SS3D.Systems.Persistence`

Layered contributor-based disk persistence replacing the monolithic tilemap JSON pipeline:

- **`PersistenceSubSystem`** + **`IPersistenceContributor`** — ordered save/load orchestrator with
  `OnBeforeCapture` / `OnAfterRestore` lifecycle events
- **`PersistenceEnvelope`** — versioned wrapper with per-domain JSON chunks via
  `EnvelopePersistenceStore`
- **Station templates** — `TileMapPersistenceContributor` + `AreaPersistenceContributor`; legacy flat
  tilemap JSON migration; template restore preserves area metadata (including `lightingSwitchOn`) when
  APCs are already placed
- **Server meta** — `PermissionsPersistenceContributor` with legacy `permissions.txt` fallback;
  append-only round history JSONL on round end
- **Wiring** — `TileSubSystem` save/load backend; server boot `LoadServerMeta`; permissions
  auto-save on `UserPermissionsChangedEvent`

EditMode tests: `PersistenceFrameworkTests`, `ServerMetaPersistenceTests`; updated
`AreaFloodFillTests` for template restore.

Merged from `archive/feature-persistence-framework`. Design:
[persistence-save.md](design/persistence-save.md). System map:
[persistence.md](architecture/systems/persistence.md). Plan:
[persistence_architecture_design_2fe61864.plan.md](plans/persistence_architecture_design_2fe61864.plan.md).

Deferred: round-config map pool contributor, Phase 2 round snapshots (items, electricity kWh,
atmospherics, substances, entities), player meta.

### Structured logging

**Paths:** `SS3D.Logging`

Serilog-based structured logging with mandatory sender + context enum, namespace-level filtering
via `LogSettings` ScriptableObject, Unity console + file sinks, and client-ID enrichment for
multiplayer. Recent work replaced remaining `Debug.Log` calls and reduced startup noise.

### Player body / animation foundation

**Paths:** `SS3D.Systems.Entities.Humanoid.Body`, `Assets/Art/Animations/`

Body-state-driven humanoid animation replacing thin Speed-blend + ad-hoc Animator calls:

- **`HumanoidBodyStateMachine`** + packed **`BodyAnimationSnapshot`** SyncVar — authoritative body /
  combat presentation state
- **`AnimationOrchestrator`** — snapshot → Animator parameters (`CombatStance`, `VelX`/`VelZ`,
  triggers); walk/run blend easing
- **Combat stance packs** — Peaceful (Locomotion Pack), Melee (Pro Melee Axe), Ranged (Basic Shooter)
  FreeformCartesian2D blends; rebuild via **SS3D → Animation → Rebuild Combat Stance Blend Trees**
- **`HumanoidIkController`** — combat look-at IK for aim yaw/pitch; melee swing on upper-body layer
- **`HumanoidCombatController`** / inventory bridge — Melee/Ranged stance from held-item traits;
  `C` toggles Peaceful ↔ combat stance

This is **stance and locomotion presentation** — hit resolution, windup, and damage live under
[Combat](#combat-phase-0-1-clean-slate-melee). Blend/timing polish remains animator-owned where
possible.

Architecture: [2026-07_player-body-animation.md](architecture/2026-07_player-body-animation.md).
Plan: [animation_system_design_250de599.plan.md](plans/animation_system_design_250de599.plan.md).
System map: [entities.md](architecture/systems/entities.md).

Merged from `feature/animation-system` onto `develop`.

**Post-merge polish** (PR #17): removed a duplicate `_bodyStateMachine` component on the ghost
controller.

**Animation polish** (PR #22, `cursor/male-injured-pack-mixamo-clips`): melee Upper Body torso
authority + Mixamo swing variants; injured limp gait from Male Injured Pack (all stances);
left-hand Upper Body mirror; arm-injury additive overlay; combat gait sync with world speed.
Shelved Misc / Probably Not clip packs for future collapse/presentation work.
Architecture: [2026-07_animation-polish.md](architecture/2026-07_animation-polish.md).

### Screen-space effects

**Paths:** `SS3D.Systems.ScreenEffects`

Client-only URP Volume overlays for diegetic feedback from [main-hud.md](design/main-hud.md) §5:

- Sustained intensities (`ScreenEffectType`): heat/cold, fire/freezing particles, low oxygen,
  dying/critical, blood-loss tunnel vision, concussion, unconscious
- Momentary melee **hit flash** via `TriggerHitFlash`
- F2 debug menu (lazy UI) and console commands (`screeneffect`, hit-flash)
- Self-bootstraps at runtime (not Boot.unity) so it can land without scene YAML edits

**Health wiring shipped** (with the health rewrite): local-owner `HealthScreenEffectMapper` drives
dying/blood-loss/oxy/concussion/unconscious from `HealthSnapshot`; hit flash on `ApplyDamage`.
Temperature/fire/frost remain debug/console-only until atmospherics wires them.

Merged via PR #6; health wiring via PR #12. Architecture:
[2026-07_screen-space-effects.md](architecture/2026-07_screen-space-effects.md).
System map: [screen-effects.md](architecture/systems/screen-effects.md).

### Health rewrite

**Paths:** `SS3D.Systems.Health`

Clean-slate rewrite per [health.md](design/health.md) / [health_implementation_plan.md](plans/health_implementation_plan.md):

- **Phases 1–5b shipped** — bleeding + bandage + VFX, asset-backed organs / pools / cardiac arrest,
  multi-threshold critical + defibrillation window, BodyParts zone resolution, field treatments
  (burn dressing, splint, O2, CPR, transfusion, antitoxin), limb severing (anatomy hide, world drops,
  head mind-swap)
- **`HumanHealthController`** + packed **`HealthSnapshot`** SyncVar; organ tick via
  `HealthSimulation` / `OrganSimulation`
- **Bleed VFX** — bone-anchored particles + URP Decal floor/body marks scaled from synced bleed rates
- **Collapse / death presentation** — ragdoll on unconsciousness, cardiac arrest, and death (interim
  RPCs; ownership refactor planned in
  [2026-07_body-presentation-authority.md](architecture/2026-07_body-presentation-authority.md))
- **Phase 0d** strips legacy health components from `Human.prefab` (do not dual-stack)

Deferred: vitals HUD / examine-self (Phase 6), stamina bridge (Phase 7a), atmosphere→lung O2 intake,
virology/chemistry modifiers.

Merged via PR #12 (`health-rewrite`). System map: [health.md](architecture/systems/health.md).

### Combat — Phase 0–1 clean-slate melee

**Paths:** `SS3D.Systems.Combat`

Clean-slate rewrite per [combat.md](design/combat.md) /
[combat_implementation_plan.md](plans/combat_implementation_plan.md) (merged via PR #23,
`cursor/combat-plan-clean-slate`). Marks the earlier thin melee vertical slice condemned and
replaces the hit path:

- **Harm primary always swings** (windup → connect → recovery + stamina) via `CmdRunMeleeSwing` —
  no collider required at click; misses still consume the full swing
- Zone damage at **connect** from the client-synced **camera** aim ray (`CmdSyncMeleeAim`),
  excluding self; fallback to body aim when no ray was synced
- Weapons: empty-hand fists (`HandMeleeExtension`), improvised held items, dedicated tool profiles
  (crowbar / hatchet / kitchen knife via `MeleeWeaponItemExtension`)
- **Intent ↔ stance:** `C` / HUD intent chip toggles Help/Harm; Harm enters combat stance;
  Harm is combat-exclusive (no fall-through to Drop / MI mid-fight)
- **Melee HUD feedback:** zone reticle lock-on recharge during recovery; white cross flash on
  successful connect (`MeleeConnectFeedback` / `MeleeRecoveryFeedback`)
- Admin test tool: console `spawndummy` (mindless Human ahead)

Deferred: disarm/grab, ranged, armor, blocking; broader combat stamina (block/fire). Stance/aim
presentation remains under entities body animation.

Plan: [combat_implementation_plan.md](plans/combat_implementation_plan.md).
System map: [combat.md](architecture/systems/combat.md).

### Vision / FOV

**Paths:** `SS3D.Systems.Vision`, `SS3D.Rendering.URP.VisionRendererFeature`

Client grid-based FOV / fog-of-war as a **hard black mask** (not soft fog):

- **`VisionSubSystem`** — `RaycastCommand` batch from `Entity.ViewPoint` → `_VisionMap`
- **`VisionRendererFeature`** composites the mask; unseen areas are fully opaque black
- Rays skip furniture/props until the nearest wall/door; triggers and inventory preview cameras
  ignored so open airlocks and dense props do not leak or stripe vision

Lives under the rendering system map ([rendering.md](architecture/systems/rendering.md)).
Merged via PR #11 (supersedes stalled `feature/vision-urp-tilemap`).

### Main HUD (UI Toolkit)

**Paths:** `SS3D.UI.MainHud`, `Assets/Content/Systems/UI/MainHud/`

Partial player HUD overlay from [main-hud.md](design/main-hud.md):

- Worn equipment (incl. gloves), gear strip, hands, intent Help/Harm
- Hand-well clicks → `HumanInventory.ActivateHand`; shown only after local spawn
- Legacy inventory / stamina-bar uGUI disabled (condemned); hold-to-self-examine deferred
- **`MainHudAssetCatalog`** via `Resources.Load` so standalone builds resolve UITK assets
  (same path-catalog pattern as machine UI — shared helper deferred under UiShell)
- Registers with `InputInterface` so pointer-over-HUD blocks world examine/selection

Merged via PR #10. System map: [inventory.md](architecture/systems/inventory.md) (player HUD slice).

### Inventory and storage redesign

**Paths:** `SS3D.Systems.Inventory`, `Assets/Content/Systems/UI/MainHud/StoragePanel/`

Clean-slate rewrite per [inventory-storage.md](design/inventory-storage.md), replacing the
placeholder inventory data model:

- **Container primitive** — real weight (recursive through nested containers), a five-tier
  size-class fit-check, stackable identical items, minimal ID-locked containers
- **On-demand storage panel** — opens per-container from the gear strip or a world object; drag-drop
  between panels and hands reuses the Tier 3 combine convention; locker access gated on the door
- **Main HUD wiring** — worn item names on HUD slots, drag-to-world drop, backpack/toolbelt/locker
  hookup, gear-strip PDA well renamed to Pocket
- **Old UI purge** — legacy inventory uGUI fully removed, not just disabled
- **Stamina bridge (Phase 7a)** — `CarriedWeight` feeds the stamina regen function

Merged via PR #16 (`claude/inventory-architecture-redesign-6q9527`). Architecture:
[2026-07_inventory-storage-redesign.md](architecture/2026-07_inventory-storage-redesign.md).
System map: [inventory.md](architecture/systems/inventory.md).

Deferred: Play Mode verification, uniform-specific pocket variance, standalone "quick loot everything."

### Comms — local speech and crowd cap

**Paths:** `SS3D.Systems.Chat` (headless `ChatSubSystem`), Main HUD local-speech UI

Vertical slice 1 of [comms.md](design/comms.md):

- **Local speech chips** — world-anchored subtitle bubbles at the speaker's head, not a chat-log line
- **Distance/occlusion tiers** and **crowd cap** — nearest speakers get bubbles, the rest compress
  into a "+N more talking nearby" chip
- **T-compose** — on-demand input box, momentary chrome, matching the intent-module fading-hint pattern
- **Always-on chat UI Phase 0 purged** — the old scrolling chat box is gone; `ChatSubSystem` runs
  headless, ready for the non-diegetic feed and PDA log to build on

Merged via PR #20 (`claude/crowd-cap-chat-plan-o8v03z`). System map:
[chat-audio-screens.md](architecture/systems/chat-audio-screens.md).

Deferred: radio/non-positional channels, whisper/shout distance tiers, non-diegetic feed (OOC/dead
chat/announcements), PDA history log, voice compatibility.

### Disposal — item network

**Paths:** `SS3D.Systems.Disposal`

Physical BFS-routed pipe network per [disposal.md](design/disposal.md), replacing BYOND's
instant off-map transit:

- **Pipe network routing** — BFS connectivity across placed pipe segments, same technique class as
  Area's flood fill and electricity's backbone
- **Chute entry** — SizeClass gate (shares inventory-storage's five-tier field, not a bespoke check)
- **Transit and outlets** — item travels the routed path at a real pace; outlet eject animation,
  spit-delay timing, and main-outlet arrival fixed for items with no wired space-ejection point
- **Interaction polish** — Dispose click priority, recycle icon registered

Merged via PR #19 (`claude/disposal-implementation-plan-4wh4hr`). Architecture:
[2026-07_disposal-item-network.md](architecture/2026-07_disposal-item-network.md).
System map: [disposal.md](architecture/systems/disposal.md).

Deferred: pipe crafting/placement UI (now designed in
[creative-mode.md](design/creative-mode.md) §4), the Cargo export hook, Phase 2 player transit.

### Input arbitration

**Paths:** `SS3D.Systems.Inputs`

Handle-based input ownership replacing the global action refcount:

- **`InputArbiter`** — contexts + suppressions return `IInputHandle`; dispose removes exactly that
  request; single writer of `InputAction.enabled`
- **`InputInterface`** — unified pointer-over-UI spanning uGUI EventSystem and UI Toolkit panels
- Gameplay and UI callers migrated off legacy `UnityEngine.Input` polling and unbalanced
  Toggle* counters

EditMode tests for the pure arbiter. Merged via PRs #8 / #9. Architecture:
[2026-07_input-arbitration.md](architecture/2026-07_input-arbitration.md).
System map: [inputs.md](architecture/systems/inputs.md).

### Headless dedicated server

**Paths:** Editor build scripts, `UNITY_SERVER` runtime guards, Docker / launch scripts

Genuine Server-subtarget Linux build (not a client launched with `-serveronly`):

- Editor menus / CI (`SS3D/Build/Dedicated Server`, `Client`) and
  `develop-release.yml` Linux build / opt-in smoke (also `multiplayer-smoke-test.yml`)
- Runtime skips Intro/Launcher, defaults `NetworkType.DedicatedServer`, disables cameras/audio and
  spawned renderers/lights under `#if UNITY_SERVER`
- Docker Compose + start scripts under `Builds/`

Known gaps: selection outline and drop interaction against a real client still broken (not
root-caused). An automated multiplayer harness now exists — see below.

Architecture: [2026-07_headless-dedicated-server.md](architecture/2026-07_headless-dedicated-server.md).

### Multiplayer test harness

**Paths:** Headless multiplayer test scripts, dedicated-server + client launch tooling

Replaces the brittle PlayMode-based multiplayer test with a real headless harness that boots a
dedicated server and one or more real clients:

- Automated round-start auth and permission seeding for smoke runs
- Log-signal detection for round lifecycle (replacing timing-based waits) plus a combined build menu
- A reconnect scenario (disconnect ownership cleanup, reconnecting a player to their own body) —
  see also `claude/player-join-leave-arch-z5zzmm`, in progress, below
- Regression coverage extended for atmospherics client-visualization sync (in progress, below)

Merged via PR #15 (`claude/multiplayer-test-harness-l90540`). Architecture:
[2026-07_multiplayer-test-harness.md](architecture/2026-07_multiplayer-test-harness.md).

Known gaps: mouse/screen-space interaction and pocket/container round-trip regressions not yet
covered; not yet verified against a real Unity build.

### Agent-first composition and UI path catalogs

Policy + first catalog wedges so agents can ship UI without scene/prefab YAML edits:

- **[Agent-first composition](architecture/2026-07_agent-first-composition.md)** — no Boot/Game
  registration for new features; no hand-edits to mega-prefabs; UITK + catalog/path pattern only;
  condemned uGUI must be replaced, not migrated
- **Machine UI path catalog** ([2026-07_mi-path-catalog.md](architecture/2026-07_mi-path-catalog.md))
  — `MachineUiAssetCatalog` loads templates by path for builds
- Main HUD copied the same Resources-catalog pattern; shared helper + full **UiShell** deferred

### Hot-path performance

**Paths:** atmospherics GPU upload, pipe networks, substances, tile coords

PR #13 cut per-tick GC on hot sim paths: `TileCoord` equality for dictionary keys, atmos upload
allocation avoidance, pipe `GetAllPlacedObject` list churn, `SubstanceContainer.AsReadOnly` on bleed
ticks. Pitfalls recorded on the atmospherics / tile / substances system maps.

---

## Design specs (future direction)

These files in [design/](design/) define **gameplay direction** (`Status: active` / `draft`).
Many systems they describe are still unbuilt on `develop`; where a foundation has shipped, the
paragraph after the table notes what landed and what remains. Implementation should follow the
specs or document explicit deviations.

| Doc | Summary |
|---|---|
| [health.md](design/health.md) | Two-tier damage: per-limb physical + organ-regulated systemic (toxin/oxy/blood volume) |
| [combat.md](design/combat.md) | Deterministic melee + weapon-intrinsic ranged accuracy; windup/recovery, blocking |
| [stamina.md](design/stamina.md) | Fast stamina pool as front-end of oxy-debt; pushing past empty draws real debt |
| [armor.md](design/armor.md) | Per-zone flat absorption; binary environmental seals hooking into organ model |
| [area.md](design/area.md) | Per-tile area partition (flood-fill + manual override) for power, access, cameras, comms |
| [comms.md](design/comms.md) | Diegetic speech subtitles with distance/occlusion; visual radio channel selector |
| [main-hud.md](design/main-hud.md) | Minimal chrome HUD — 3D body vitals, cut targeting doll, intent chording |
| [hacking-interface.md](design/hacking-interface.md) | Field diagnostic unit (FDU) — diegetic 7-panel tool; discovery by physical access |
| [objectives.md](design/objectives.md) | Steal/assassinate/escape resolved against real system state (possession, death, Area occupancy) — no hidden roll |
| [round-end.md](design/round-end.md) | Reveal, summary, and transition; evac shuttle call/countdown/point-of-no-return |
| [creative-mode.md](design/creative-mode.md) | In-game map editor: object placement, utility routing, bulk tools, undo, spawn-point authoring |
| [persistence-save.md](design/persistence-save.md) | Four-layer save model — station templates, server meta, player meta, automatic round-snapshot recovery |
| [admin-tools.md](design/admin-tools.md) | Permission tiers, ahelp, stealth observation, world intervention, moderation — every action logged |
| [cryogenics.md](design/cryogenics.md) | Physical cryo pod as a real pause mechanic for logged-off characters, distinct from death/respawn |
| [disposal.md](design/disposal.md) | Physical BFS-routed pipe network replacing instant off-map transit; Cargo export hook |
| [inventory-storage.md](design/inventory-storage.md) | One Container primitive for backpacks/crates/lockers — weight, size-class fit, stacking, locks |
| [networking.md](design/networking.md) | Connection lifecycle — disconnect, reconnect, capacity (PR #21) |
| [audio.md](design/audio.md) | Diegetic SFX, per-area ambience, personal cues (PR #21) |
| [player-accounts.md](design/player-accounts.md) | Delegated identity model closing deferred account gaps (PR #21) |
| [antagonist-content.md](design/antagonist-content.md) | Traitor uplink, Malf-AI, Nuclear Operatives MVP team gamemode (PR #21) |

Machine interfaces, the radial interaction menu, screen-space overlays, and the Main HUD overlay
partially implement [main-hud.md](design/main-hud.md) (tiered interactions, intent, diegetic machine
control, Volume-based screen feedback, gear/hands strip + melee zone reticle — drag-combine Tier 3,
vitals cluster, and atmos wiring for screen effects still pending). **Area foundation** partially
implements [area.md](design/area.md) (APC-seeded flood-fill, area-scoped power/lighting, air-alarm
area device discovery — live mutation recompute and editor merge/split still pending).
**ID / access foundation** partially implements [id-access.md](design/id-access.md) (crew records,
door and machine UI gates, ID console — auth logs and broader design coverage still pending).
**Health rewrite** partially implements [health.md](design/health.md) (Phases 1–5b; Phase 6+
deferred). **Combat** partially implements [combat.md](design/combat.md) (Phase 0–1 clean-slate
melee; Phases 2–7 pending). **Player body animation** implements stance/locomotion plus injured
limp polish; it is not full combat. **Vision FOV** has no dedicated design doc — it is a rendering
feature under [rendering-lighting.md](design/rendering-lighting.md) territory. **URP lighting
Phase 1** partially implements that same lighting look; plate polish remains open.
**Persistence foundation** partially implements [persistence-save.md](design/persistence-save.md)
(station templates and server meta shipped; player meta and the automatic round-snapshot
crash-recovery safety net that doc designs are still pending). **Comms** partially implements
[comms.md](design/comms.md) (local speech chips, distance/occlusion, crowd cap; radio/channels,
non-diegetic feed, and PDA log still open). **Disposal** partially implements
[disposal.md](design/disposal.md) (BFS pipe routing and transit shipped; pipe placement now has a
real path via [creative-mode.md](design/creative-mode.md) §4, but the Cargo export hook and Phase 2
player transit remain). **Inventory and storage redesign** partially implements
[inventory-storage.md](design/inventory-storage.md) (Container primitive, on-demand panel, Main HUD
wiring, and the stamina bridge shipped; Play Mode verification pending). **Map Editor** partially
implements [creative-mode.md](design/creative-mode.md) (admin full-screen authoring shipped;
Builder role, instant place, and round-config pool deferred). The rest of these specs remain
design-only.

---

## In progress on feature branches

Work that has **not** merged to `develop` yet. Branches drift from `develop` quickly — rebase
before assuming commit counts.

| Branch | Ahead / behind `develop` | System | Notes |
|---|---|---|---|
| `claude/icon-stack-impl-amql37` | 9 / 0 | Main HUD / Health | Alert stack icons wired to `HealthSnapshot`; purges remaining uGUI vitals chips |
| `claude/crafting-system-plan-uyc4of` | 3 / 101 | Crafting | Phase 0 purge + Phase 1 freeform recipe / Tier 3 combine; rebuild of condemned crafting |
| `claude/human-prefab-debt-plan-u1199p` | 1 / 0 | Entities | Plan for `Human.prefab` mega-prefab decomposition (TECH_DEBT §1.1) |
| `claude/atmospherics-client-viz-sync-ejkbw4` | 3 / 137 | Atmospherics | Client-visualization sync Phase 1 + harness regression coverage ([effort doc](architecture/2026-07_atmos-client-visualization-sync.md)) |
| `claude/player-join-leave-arch-z5zzmm` | 3 / 154 | Networking | Disconnect ownership cleanup + reconnect-to-own-body; adds a reconnect scenario to the multiplayer harness |
| `claude/vitals-scan-result-alt-ui-ez50ba` | 2 / 210 | Health / Main HUD | Health scanner machine interface (alternate vitals-scan-result UI) |
| `claude/ui-toolkit-architecture-ybt2qv` | 1 / 164 | UI shell | UI Toolkit shell scaffolding; migrates radial/armed interaction UI onto it |
| `feature/map-editor-replacement` | 10 / 397 | TileMap Creator / creative | Click-to-place and editor input work; **superseded** by shipped Map Editor (PR #26) — safe to abandon |
| `feature/tilemap-overlay` | 2 / 390 | Tilemap visuals | Area floor stripes + sparse decals — partially overtaken by Map Editor overlays / Area mesh stripes on `develop` |
| `feature/character-creator` | 3 / 387 | Character customizer | Layout + URP preview polish |
| `feature/inventory-storage` | 9 / 507 | Inventory + storage UI | Superseded by the shipped inventory redesign (PR #16); likely safe to abandon |
| `feature/urp-lighting-phase1` | 2 / 518 | URP lighting | Superseded by shipped Phase 1 (PR #24 / `feature/urp-lighting-phase1-v2`) |

Merged and retired from this table: `health-rewrite` (PR #12), vision FOV (PR #11; old
`feature/vision-urp-tilemap` abandoned), `feature/performance-improvements` (PR #13),
`feature/mi-path-catalog` (catalog wedge on `develop`), `cursor/diegetic-ui-backdrop-blur` (PR #14),
`claude/multiplayer-test-harness-l90540` (PR #15), `claude/inventory-architecture-redesign-6q9527`
(PR #16), `cursor/fix-airlock-close-and-apc-area-load` (PR #17), `claude/design-doc-alignment-audit-wsyn1d`
(PR #18), `claude/disposal-implementation-plan-4wh4hr` (PR #19), `claude/crowd-cap-chat-plan-o8v03z`
(PR #20), `claude/design-docs-integration-n072cq` (PR #21), `cursor/male-injured-pack-mixamo-clips`
(PR #22), `cursor/combat-plan-clean-slate` (PR #23), `feature/urp-lighting-phase1-v2` (PR #24),
`claude/game-architecture-debt-cakjgx` (PR #25), `claude/map-editor-creative-mode-8pp6qj` (PR #26).

---

## Archived branches merged into `develop`

These `archive/*` branches were feature-complete enough to merge and are kept for history.
Do not develop on them — use `develop` or a new feature branch.

| Branch | Merged | Primary deliverable |
|---|---|---|
| `archive/develop-unity6` | 2026-07 | Unity 6 upgrade umbrella |
| `archive/feature-urp-migration` | (via develop-unity6) | URP 17 pipeline |
| `archive/feature-1387-selection-api` | (via develop-unity6) | Shader selection API |
| `archive/feature-1394-detailed-examine` | (via develop-unity6) | Detailed examine UI |
| `archive/feature-tilemap-system-refactor` | (via develop-unity6) | Adjacency engine + construction |
| `archive/feature-game-lifecycle-hardening` | (via develop-unity6) | Round state machine |
| `archive/fix-selection-camera-picking` | (via develop-unity6) | Cursor picking alignment |
| `archive/machine-ui` | 2026-07 | Machine interface UI (APC/SMES) |
| `archive/feature-diegetic-screen-ui-framework` | 2026-07-11 | Diegetic device shell, component library, vending machine UI |
| `archive/feature-examine-localization` | 2026-07-09 | Unified Examine localization + `LocalizedTextService` |
| `archive/feature-interaction-system-hardening` | 2026-07-09 | Interaction RPC hardening, pipeline, outlines, radial menu |
| `archive/feature-radial-menu-redesign` | (via interaction-system-hardening) | UI Toolkit three-tier radial menu + armed overlay |
| `archive/areas-foundation` | 2026-07-12 | APC-seeded areas, kWh power model, area lighting + consumer visuals |
| `archive/feature-atmos-ecs` | 2026-07-12 | ECS turf gas sim, plasma combustion, GPU fog/fire visuals |
| `archive/feature-interactions` | 2026-07-12 | Post-hardening interaction/radial polish + FastScriptReload removal |
| `atmos-pipes` | 2026-07-14 | Gas pipe networks, port devices, atmos machine UIs, APC/SMES diegetic redesign |
| `archive/tilemap-layer-visibility` | 2026-07-14 | Client-only tilemap build-menu layer visibility toggles |
| `archive/feature-id-access-foundation` | 2026-07-14 | Crew records, credential resolver, door/machine ID access gates |
| `archive/feature-persistence-framework` | 2026-07-14 | Contributor-based persistence: station templates + server meta |

Recent `develop` merges that did **not** use an `archive/*` branch rename (GitHub PRs / direct
feature merge): EditMode test race (#1), docs structure redesign (#2), Unity license CI cleanup (#3),
vending polish (#4), selection/outline flicker (#5), screen-space effects (#6), Simple Toon palette
emission (#7), input arbitration (#8 / #9), Main HUD UITK (#10), vision FOV hard mask (#11), health
rewrite + early combat melee + screen-effect health wiring (#12), hot-path performance (#13), player
body animation (`feature/animation-system`), headless dedicated server tooling, agent-first
composition policy, machine UI / Main HUD path catalogs, diegetic UI backdrop blur (#14),
multiplayer test harness (#15), inventory and storage redesign (#16), airlock/APC +
selection/interaction bugfix batch (#17), design/implementation alignment audit (#18), disposal
item-network (#19), comms local-speech and crowd-cap (#20), design-docs integration —
antagonists/audio/networking/accounts (#21), animation polish / injured limp (#22), combat Phase
0–1 clean-slate melee (#23), URP lighting Phase 1 (#24), TECH_DEBT tracker (#25), Map Editor
replacement (#26).

---

## What upstream still has that we don't automatically inherit

- Official releases and the [ss3d.space](https://ss3d.space/) download channel (this fork's
  prereleases are manual GitHub Actions cuts, not the same channel)
- GitBook documentation and devblogs
- Active milestone/project board automation
- Discord CI notifications
- Whatever lands on `upstream/develop` after our fork point (currently **0** commits behind as of
  2026-07-21)

When pulling from upstream, expect conflicts in: ProjectSettings, materials/shaders, render
pipeline config, tilemap/construction code, interaction/selection code, and any system listed
above as shipped.

---

## Standing offer

Anything here can be proposed upstream on request. Open an
[issue](https://github.com/henkhooft/SS3D/issues) or
[discussion](https://github.com/henkhooft/SS3D/discussions) and a PR-shaped version of whatever
is relevant can be prepared. Large divergences (Unity 6, URP, tilemap refactor) are best proposed
as incremental slices rather than one mega-PR.

---

## Maintaining this document

Update when:

- A feature branch merges to `develop` (move it from "In progress" to "Shipped")
- A new design spec or architecture doc lands
- Unity/toolchain versions change
- GitHub automation or repo presentation changes
- Significant divergence from upstream develops (re-fetch `upstream` and update commit counts)

After updating, bump the **Last updated** date at the top.
