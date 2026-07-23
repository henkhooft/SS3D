# Technical debt tracker

**Last updated:** 2026-07-23

This is the project-wide register of architecture problems, code smells, and quality risks that
threaten long-term viability rather than one-off bugs. It is a cross-cutting **reference** doc, not
one of the four layers in [SKILL.md](../SKILL.md) — it doesn't replace the per-domain debt notes
already living in system maps (`Documents/architecture/systems/*.md` **Pitfalls** / **Architecture
smells** sections) or in [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md);
it indexes and ranks them so "what's the biggest structural risk right now" has one answer instead
of 40 scattered ones.

**Scope:** structural debt — things that make the codebase harder to extend safely, not missing
features (those live in [INDEX.md](INDEX.md)'s coverage table) and not gameplay-design gaps (those
live in each domain's design doc `§Out of scope`).

**Maintaining this doc:** update when a debt item is paid down (move to "Resolved" with the PR/date)
or when a new structural smell is found during feature work — same trigger as `update-system-docs`.
Cite system maps by path + section rather than re-explaining; this doc should stay skimmable, not
become a second copy of the maps.

---

## How to read the severity column

- **Blast radius** — how much code/how many systems break or must be touched if this goes wrong or
  ever gets fixed.
- **Trend** — getting worse (more code piles on it), stable (contained by convention), or scheduled
  (a named follow-on effort exists).

---

## 1. Top structural risks (ranked)

### 1.1 `Human.prefab` mega-prefab

**Blast radius: highest — trend: stable by policy, not by tooling**

`Assets/Content/WorldObjects/Entities/Humanoids/Human/Human.prefab` is ~15.6k lines with 126
distinct `m_Script` component references. Health, inventory, movement, combat, body animation, and
entity identity are all bolted onto one GameObject graph that agents (and humans) cannot safely
hand-edit — a wrong YAML edit silently desyncs `fileID` references with no compiler error.
[2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) makes this a named
prefab-composition-debt item and forbids "add one more behaviour" as a feature path, and the health
rewrite's Phase 0d demonstrated the only accepted mitigation (strip-and-rewire via Editor tooling,
not organic growth). Follow-on **(d) Entity prefab setup / recipes** (named in the same doc) is now
partly paid down: [2026-07_human-prefab-decomposition.md](2026-07_human-prefab-decomposition.md)
turned strip-and-rewire into a repeatable `PrefabUtility` recipe convention
(`HumanPrefabHygiene`/`BodyPartContainerInteractiveStrip`/`HumanPrefabRecipes`), added a CI-checked
missing-script/dev-hack/`ContainerInteractive` gate (`HumanPrefabIntegrityTests`, passing), and fixed
the concrete hygiene bugs the audit found (stale missing-script GUID caches, the never-built
`BodyPartContainerInteractiveStrip.cs` tool `inventory.md`/`combat.md` cited, a dev-only hack shipping
on production `Human.prefab`). **Enforcement is now partially machine-checked** (the EditMode test
catches regressions on those specific items) but still not comprehensive — nothing stops a future PR
from adding a 127th *unrelated* component; only the denylisted/known-bad ones are caught. The organ
prefab-ization originally planned turned out to target the wrong thing (see the effort doc's Phase 1)
and was deprioritized. Phase 3 (domain strip-and-rewire) has its first instance done: `Hands.PlayerHands`
on `Human.prefab` — previously hand-dragged `fileID`s with no recipe tool — is now managed via
`HandsPrefabSetup` (**SS3D → Inventory → Wire Human Hands**). Every other domain directly on
`Human.prefab` (movement/animation, combat, comms, examine, stamina, substances) remains scheduled, not
forced — pick up each when its own redesign next touches entity wiring.

- Related: [entities.md](systems/entities.md) § Prefab composition debt, [health.md](systems/health.md), [combat.md](systems/combat.md) (`spawndummy` reuses the same prefab), [2026-07_human-prefab-decomposition.md](2026-07_human-prefab-decomposition.md)

### 1.2 Collapse/death/ragdoll presentation has no single owner

**Blast radius: high — trend: scheduled but not started**

Health, `Ragdoll`, `AnimationOrchestrator`, `HumanoidBodyStateBridge`, and movement controllers each
independently write "is this body collapsed/dead" behavior. The interim implementation leans on
transport quirks as control flow — `ServerRpc` from server is a no-op, SyncVar `OnChange` may not
fire on the server, `OnDisable` during network teardown is not "recover" — each of which has already
caused a shipped bug (death re-triggering every tick, ghost stack-overflow, corpses standing back up,
unconscious players walking). [2026-07_body-presentation-authority.md](2026-07_body-presentation-authority.md)
documents the target architecture (single replicated `BodyPresentationState` + one applier) and
explicitly says **do not add a third collapse path** while it's pending — but it is `Status: planned`
with no owner or date, so every new health/combat feature that touches consciousness is one incident
away from adding that third path anyway.

- Related: [health.md](systems/health.md) § Pitfalls, [entities.md](systems/entities.md) § Body presentation debt

### 1.3 Interaction `Discover` has no contract

**Blast radius: high — trend: stable (bandaged repeatedly, not fixed)**

[interactions-framework.md](systems/interactions-framework.md) § Architecture smells names this
directly: some interaction-source extensions always `Add` a candidate interaction (`Drop`), others
gate on `CanInteract` at discover time; source-only and target-bound entries share one list with no
type-level distinction; `InteractionEvent.Point` uses `Vector3.zero` as a sentinel for "unresolved,"
indistinguishable from a real hit at world origin. This is the root cause of at least two shipped
bugs fixed by narrow bandages instead of a contract change: empty-hand outline pollution from
`Craft` (see 1.6) and wall-mount interactions that silently ignored range because a missing collider
left `Point` at the sentinel value. Every new `IInteractionSourceExtension` is a coin flip on which
convention it should follow because the framework doesn't enforce one.

- Related: [interactions-framework.md](systems/interactions-framework.md) § Architecture smells (full list), [interactions-runtime.md](systems/interactions-runtime.md)

### 1.4 UI asset-catalog pattern copy-pasted three times, no shared infrastructure

**Blast radius: medium, compounding — trend: getting worse**

Machine UI, Main HUD, and the Storage Panel each ship their own `*AssetPaths` constants class +
`*AssetCatalog` ScriptableObject + a dedicated Editor "Rebuild Asset Catalog" menu item, because
[ui-shell.md](systems/ui-shell.md) (the intended shared composition root) has stayed `Status: stub`
across every UI effort that has shipped since it was proposed. Each copy independently reinvents the
same failure mode (stale catalog after adding a UXML path without remembering to run the rebuild
menu) and the same fix. A fourth UI surface needing this pattern is very likely before UiShell lands,
at which point it's four copies to migrate instead of one.

- Related: [ui-shell.md](systems/ui-shell.md) § Future work, [machine-interface.md](systems/machine-interface.md), [inventory.md](systems/inventory.md)

### 1.5 One-off Editor rebuild-menu proliferation (data-codegen)

**Blast radius: medium — trend: getting worse**

[data-codegen.md](systems/data-codegen.md) § Architecture smells names this explicitly: feature work
keeps landing a new `MenuItem` that clones/rewrites assets and registers them (interaction icon
sprites, the three catalog builders in 1.4), each with its own GUID-preservation hacks and "did
anyone remember to run this" drift, instead of one shared import → Addressables →
`AssetDatabase.LoadAssetsFromAssetGroup` → codegen pipeline. No CI check verifies a committed catalog
asset is in sync with the C# path constants it should mirror — drift is discovered at Play Mode/build
time, not at PR time.

- Related: [data-codegen.md](systems/data-codegen.md)

### 1.6 Crafting is dead code that hasn't been deleted

**Blast radius: low, but pure waste — trend: not scheduled**

`CraftingSubSystem`, `Craft.cs` (on both hand prefabs), and the crafting menu uGUI have been called
"obsolete, due for removal" across three separate docs
([INDEX.md](INDEX.md), [crafting.md](systems/crafting.md),
[interactions-framework.md](systems/interactions-framework.md) § Architecture smells #1) for multiple
work sessions. `Craft` on hands is a standing landmine: left in place, it silently pollutes hover
outlines for every empty-handed target unless discover-time gating (a bandage, not a fix) stays
correct. Nobody has scheduled the actual deletion PR (remove `Craft` from hand prefabs, delete the
subsystem, drop the codegen recipe refs) even though the cost of leaving it is now higher than
deleting it.

- Related: [crafting.md](systems/crafting.md), [interactions-framework.md](systems/interactions-framework.md)

### 1.7 Legacy scene-based subsystem registration coexists with three ad-hoc bootstrap styles

**Resolved 2026-07-23** for gameplay SubSystems — see [§6 Resolved](#6-resolved). Residual UI-host
`RuntimeInitializeOnLoad` self-bootstraps (UiShell / MainHud / StoragePanel) stay under agent-first
follow-on **(b)**, not this item.

### 1.8 Condemned-UI backlog: 6+ live uGUI surfaces still shipping

**Blast radius: medium — trend: shrinking slowly**

Confirmed still condemned-but-present on `develop`: console panel
([ingame-console.md](systems/ingame-console.md)), lobby job-select/ready UI
([rounds-lobby.md](systems/rounds-lobby.md)), the ScreenEffects F2 debug canvas
([screen-effects.md](systems/screen-effects.md)), TileMap Creator
([tile.md](systems/tile.md)), the crafting menu (1.6), and examine's hover/detailed uGUI panels
([examine.md](systems/examine.md)). Each is "do not extend, replace when the owning redesign lands,"
which is the right call individually, but there is no single burndown tracking how many of these
are left or in what order they should go — six live legacy UI stacks is real maintenance surface
(input arbitration, click-through, and pointer-over-UI code all still have to account for them).

- **Doc-hygiene note:** [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md)'s
  Condemned UI table still lists "Inventory / hands / intent uGUI" as condemned-pending-replacement,
  but [inventory.md](systems/inventory.md) confirms that surface's "old UI purge" already shipped
  (uGUI fully removed, not just disabled) as part of PR #16. That row is stale and should be removed
  next time that doc is touched.

### 1.9 God-classes forming in hot UI/interaction code

**Blast radius: medium — trend: getting worse**

`InteractionController.cs` (1,471 lines) owns primary-click routing, radial menu dispatch, Help/Harm
intent sync, combat-stance switching, Harm melee swing dispatch, armed-mode resolution, *and* hover
outline feedback — seven distinct responsibilities in one `NetworkBehaviour`. `MainHudSubSystem.cs`
(1,136 lines) similarly composes equip/unequip, gear, hands, intent chip polling, zone-reticle input,
and HUD/MI visibility suppression in one class. Both are the busiest, most-edited files in their
respective systems (combat and inventory both landed features here in the same week per
[FORK_STATUS.md](../FORK_STATUS.md)), which is exactly the profile that produces merge conflicts and
regressions when two features touch the same god-class at once. Neither has a decomposition plan.

- Related: [interactions-runtime.md](systems/interactions-runtime.md), [inventory.md](systems/inventory.md)

### 1.10 Play Mode / multiplayer verification is optional in practice

**Blast radius: high — trend: stable, structurally hard to fix**

There is no CLI build/test flow ([CLAUDE.md](../../CLAUDE.md) says so directly) — everything runs
through the Unity Editor, and CI ([editmodetestrunner.yml](../../.github/workflows/editmodetestrunner.yml))
only exercises EditMode tests. Several recently-merged, currently-shipping systems explicitly flag
that they were never run in Play Mode before merging: the inventory/storage redesign
([inventory.md](systems/inventory.md) — "Play Mode / Editor verification still required... compile/Play
Mode not run in implementing session"), and the multiplayer test harness itself
([2026-07_multiplayer-test-harness.md](2026-07_multiplayer-test-harness.md) — "not yet verified
against a real Unity build"). The headless dedicated server has a known, un-root-caused bug (selection
outline and drop interaction break against a real client) that has shipped for multiple sessions
because nothing in CI would catch it. This isn't one bug — it's a structural gap where "the tests
pass" and "a human pressed Play once" are different, unenforced bars.

- Related: [networking-session.md](systems/networking-session.md), [2026-07_headless-dedicated-server.md](2026-07_headless-dedicated-server.md)

### 1.11 StyleCop disabled repo-wide with no replacement gate

**Blast radius: low today, compounding — trend: stable**

StyleCop analyzers were disabled project-wide on 2026-07-14 "to reduce friction on AI-assisted
edits" ([FORK_STATUS.md](../FORK_STATUS.md) § Analyzer tooling). `.editorconfig` still encodes the
naming/formatting rules, but nothing enforces them in CI — conformance now depends entirely on
reviewer attention (human or AI) at PR time, with no automated signal when a change drifts from
convention. As the volume of AI-assisted PRs grows, this is the kind of gate that's cheap to skip
early and expensive to reinstate once thousands of lines have drifted.

### 1.12 Scene-wide `FindObjectsByType` calls in gameplay code, not just Editor tooling

**Blast radius: low-medium — trend: improving for locator; Item/Locker scans unchanged**

Ten call sites use `FindObjectOfType`/`FindObjectsByType` for O(n) scene scans. Most are legitimately
Editor-only (catalog builders, debug gizmo drawers) or dev-bypass toggles, which is fine. Three are
not: `TileMap.cs` scans every `Item` in the scene (`FindObjectsOfType<Item>()`), `Locker.cs` scans
every `ContainerViewer`, and — more structurally — `Core/Subsystems.cs`, the service locator every
domain depends on, falls back to `FindObjectOfType` when a subsystem isn't in its registry cache.
The locator fallback is now skipped while quitting **and** while `WaitingForServer`
([2026-07_session-world-lifecycle.md](2026-07_session-world-lifecycle.md) Phase 3 scaffolding);
prefer `TryGet` + ready. Hot-path Item/Locker scans remain.

- Related: [core-subsystems.md](systems/core-subsystems.md), [tile.md](systems/tile.md), [2026-07_session-world-lifecycle.md](2026-07_session-world-lifecycle.md)

### 1.13 Hotkey-bound debug UIs sprawl with no shared shell

**Blast radius: medium — trend: getting worse**

Dev/debug surfaces are accumulating as one-off function-key toggles instead of a single debug layer
under [ui-shell.md](systems/ui-shell.md). Current map (not exhaustive — more will land the same way):

| Key | Surface | Notes |
|---|---|---|
| F2 | `ScreenEffectsDebugMenuView` | Condemned uGUI canvas; Keyboard-polled |
| F3 | `LocalSpeechDebugTrigger` | Cycles local chat test lines; Keyboard-polled; must respect `InputInterface.IsCapturingText` |
| F4 | `AlertStackDebugMenuView` | UITK + arbitrated `ToggleAlertStackDebug`; moved off F3 after colliding with speech |
| P | Atmos debug overlay | `Other/Toggle Atmos Debug` (+ Keyboard fallback) |
| (other) | Selection debug, health H, etc. | Same pattern: domain-owned bootstrap + ad-hoc chord |

Problems this creates: **key collisions** (alert stack and speech both wanted F3 until one moved),
**inconsistent input paths** (raw `Keyboard.current` vs code-defined `InputSubSystem` actions vs
`Controls.inputactions`), **no inventory of what's bound** so the next feature guesses another F-key,
and **no shared PanelSettings/theme/bootstrap** (blank UITK `PanelSettings` already caused invisible
labels on the alert menu). Console commands (`screeneffect`, `alertstack`, …) are the durable debug
API; the hotkey panels are convenience debt until UiShell owns a debug layer.

**Do not** add another F-key panel without (a) checking this table + [inputs.md](systems/inputs.md)
and (b) preferring an in-game console command first. Target: one arbitrated debug overlay host under
UiShell that registers chords centrally; delete or fold F2/F3/F4 panels when that lands.

- Related: [inputs.md](systems/inputs.md), [screen-effects.md](systems/screen-effects.md), [chat-audio-screens.md](systems/chat-audio-screens.md), [inventory.md](systems/inventory.md) (alert F4), [ingame-console.md](systems/ingame-console.md), [ui-shell.md](systems/ui-shell.md)

### 1.14 Asset/file organization drift (icons scattered across 8+ locations)

**Blast radius: low individually, high in aggregate — trend: scheduled but not started**

The intended `Assets/Art/` (raw art, by type then domain) vs. `Assets/Content/` (game data/composition) split
is sound, but a full audit found icon image assets alone living in at least eight separate locations
(`Art/Graphics/UI/Misc/Heroicons`, `Art/Graphics/UI/Interactions/InteractionIcons`,
`Art/Graphics/UI/Containers/InventoryIcons`, `Art/Graphics/Misc/RenderedIcons`,
`Content/Systems/UI/MainHud/Icons/AlertStack`, plus a folder-name collision between the Art-side
`InteractionIcons/` PNGs and a `Content/Systems/UI/Systems/Interactions/InteractionIcons/` ScriptableObject
catalog of the same name), five separate "Misc" dumping-ground folders, and no written rule for when a
ScriptableObject belongs in `Content/Data/` versus next to the system that owns it. Left alone, every new
feature adds another icon folder or another Misc bucket instead of following a convention, because no
convention was ever written down.
[2026-07_asset-file-structure-taxonomy.md](2026-07_asset-file-structure-taxonomy.md) documents the full audit,
the target taxonomy, and a 3-phase migration plan (docs → low-risk renames → reference-sensitive moves); Phase
0 (docs/tooling) has landed, including a CI-enforced guardrail — `AssetTaxonomyTests.cs` (EditMode, runs on
every PR via the existing `editmodetestrunner.yml`) fails on any *new* raw-art file under `Content/`, icon
image outside `Art/Icons/`, "Misc" folder, or first-party asmdef outside `Scripts/`; all current violations are
explicitly grandfathered so this only blocks fresh drift, not the existing backlog. Phases 1–2 (the physical
moves) are not started.

- Related: [asset-organization.md](systems/asset-organization.md), [data-codegen.md](systems/data-codegen.md)
  § Architecture smells (same one-off-Editor-menu root cause)

### 1.15 Addressables configured but unused — every asset eager-loaded into RAM

**Blast radius: high (whole-game memory footprint) — trend: scheduled but not started**

21 Addressables groups are configured under `Assets/Content/Addressables/` (Items, Materials,
Sounds, InteractionIcons, CraftingRecipes, UIElements, etc.), but they are only ever used as an
editor-time curation source: `AssetDatabase.LoadAssetsFromAssetGroup()` copies each group entry's
real `Object` reference into a serialized dictionary, and `Assets.Get<T>` is a synchronous read
against those hard references. No `Addressables.LoadAssetAsync`/`Instantiate*` call exists anywhere
in `Assets/Scripts` — grepped and confirmed 2026-07-21. Every configured asset is therefore pulled
into RAM the moment its owning database loads and stays resident for the process lifetime, the exact
problem upstream tracks as [RE-SS3D/SS3D#1494](https://github.com/RE-SS3D/SS3D/issues/1494)
("excessive memory usage," up to 600MB) with an open, unreviewed rewrite attempt at
[RE-SS3D/SS3D#1500](https://github.com/RE-SS3D/SS3D/pull/1500) — this fork has the same root cause,
reached via a different (Addressables-group-as-metadata) route. Migration scoped in
[2026-07_addressables-expansion-migration.md](2026-07_addressables-expansion-migration.md); not yet
started.

- Related: [data-codegen.md](systems/data-codegen.md) § Architecture smells #2

### 1.16 Session/world lifecycle — shipped; optional backoff remains

**Resolved 2026-07-23** — see [§6 Resolved](#6-resolved). Optional reconnect exponential backoff and
UI-host consolidation remain deferred elsewhere (not reopen criteria for this item).

---

## 2. Hot-path GC / performance debt (tracked, partially paid down)

PR #13 fixed a first wave of per-tick allocation ([FORK_STATUS.md](../FORK_STATUS.md) § Hot-path
performance): `TileCoord` boxing on dictionary lookups, atmos GPU-upload allocations, pipe-network
list churn, and `SubstanceContainer.AsReadOnly()` on bleed ticks. The pattern keeps recurring in new
code rather than being structurally prevented (no analyzer/test flags a new per-tick `List.AsReadOnly()`
or `new float[]` in a hot loop) — treat every new tick-driven system (atmospherics ports, area
recompute, electricity distribution) as needing the same review pass PR #13 did, not as automatically
safe because "the pattern was fixed once."

- Full list of pitfalls-with-fixes: [tile.md](systems/tile.md), [atmospherics.md](systems/atmospherics.md), [substances.md](systems/substances.md) § Pitfalls

---

## 3. Prefab / mega-object composition debt (tracked in [agent-first-composition.md](2026-07_agent-first-composition.md))

That doc is the canonical register for condemned UI and prefab composition debt (§ Condemned UI, §
Prefab composition debt tables). Don't fork a duplicate list here — see 1.1 and 1.8 above for the
two items from that table with the highest current risk, and check that doc directly for the full
tables before starting any redesign that touches `Human.prefab` or a condemned surface.

---

## 4. Known correctness bugs living in shipped infrastructure

- **Dedicated server vs. real client:** selection outline and drop interaction break when a real
  client connects to a dedicated server build; not root-caused despite the headless server having
  shipped for multiple sessions. [2026-07_headless-dedicated-server.md](2026-07_headless-dedicated-server.md).
- **Multiplayer harness gaps:** mouse/screen-space interaction and pocket/container round-trip are
  not covered by the automated harness yet, so regressions there ship silently.
  [2026-07_multiplayer-test-harness.md](2026-07_multiplayer-test-harness.md).
- **`C` is double-bound:** Input System's Cancel Interaction and combat's Help/Harm toggle both
  hardcode the same key; a rebind is deferred, not fixed. [interactions-runtime.md](systems/interactions-runtime.md), [combat.md](systems/combat.md).

---

## 5. Accepted fork deviations (not debt — do not "fix" without a design change)

Several intentional divergences from design docs are already recorded as accepted, not scheduled for
rework, each directly in its owning system map: Main HUD fully hides rather than layers under machine
UI ([inventory.md](systems/inventory.md) § Fork deviation), Area is APC-seeded rather than
auto-detected ([area.md](systems/area.md)), combat's primary click always swings rather than requiring
a hover target ([combat.md](systems/combat.md)), and examine always shows the name tooltip on plain
hover rather than gating everything behind the hold key ([examine.md](systems/examine.md)). Listed
here only so this doc doesn't accidentally get read as "these are bugs" — they are direction calls,
not quality problems.

---

## 6. Resolved

*(Move items here with the PR/commit that closed them, so the register shows real progress rather
than only growing. Keep a one-line stub under the old §1.x number so external citations still resolve.)*

### 1.7 Legacy scene-based subsystem registration (gameplay) — 2026-07-23

Gameplay SubSystems are code-owned: `SystemsBootstrap` (DDOL process-wide) + `NetworkSystemsHub`
(Online spawn). Boot/Game no longer place per-system SubSystem GameObjects
([2026-07_session-world-lifecycle.md](2026-07_session-world-lifecycle.md) Phase 3h).

- **Closed by:** `b74f47123` (Phase 3h hub + scene strip); post-ship hardening `ab79afee2` (ServerMeta
  boot ownership), `b57f3974e` (PlayerCamera lazy resolve); smoke hardening (Selection/Armed
  `TryGet`, Disconnecting suppress).
- **Not in this close:** UiShell / MainHud / StoragePanel `RuntimeInitializeOnLoad` — agent-first
  follow-on **(b)**. Also residual: content-prefab `SubSystem`s (PlayerCamera / Radial / Armed /
  MapEditor) that still register when Game loads before hub Online — consumers use `TryGet` until
  those move to bootstrap/hub ([2026-07_session-world-lifecycle.md](2026-07_session-world-lifecycle.md)).
- Related: [core-subsystems.md](systems/core-subsystems.md), [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md)

### 1.16 Session/world lifecycle — 2026-07-23

Session FSM, Empty offline, world-readiness graph, `PrepareRound` gate, `SystemsBootstrap`, and
`NetworkSystemsHub` shipped under
[2026-07_session-world-lifecycle.md](2026-07_session-world-lifecycle.md) (`Status: shipped`).

- **Closed by:** `77f4d9798` / `84401b2fe` / `b74f47123` (phases 1–3h); follow-up Play Mode fixes on
  `cursor/session-world-lifecycle` as above.
- **Deferred elsewhere (do not reopen this item):** reconnect exponential backoff; UI-host bootstrap
  consolidation.
- Related: [networking-session.md](systems/networking-session.md), [core-subsystems.md](systems/core-subsystems.md)
