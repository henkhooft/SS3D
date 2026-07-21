# Technical debt tracker

**Last updated:** 2026-07-21

This is the project-wide register of architecture problems, code smells, and quality risks that
threaten long-term viability rather than one-off bugs. It is a cross-cutting **reference** doc, not
one of the four layers in [SKILL.md](SKILL.md) — it doesn't replace the per-domain debt notes
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
not organic growth). But enforcement is **convention only** — nothing stops a future PR from adding
a 127th component. Follow-on **(d) Entity prefab setup / recipes** (named in the same doc) that
would make even the rewire tool-mediated has never been scheduled.

- Related: [entities.md](systems/entities.md) § Prefab composition debt, [health.md](systems/health.md), [combat.md](systems/combat.md) (`spawndummy` reuses the same prefab)

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

**Blast radius: medium — trend: getting worse**

[core-subsystems.md](systems/core-subsystems.md) calls Boot/Game scene-placed subsystem registration
"legacy," with code bootstrap as the stated target
([2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) follow-on **(a)**,
unscheduled). In the meantime, individual systems have each invented their own escape hatch:
`ScreenEffectsSubSystem` self-bootstraps via `RuntimeInitializeOnLoadMethod`; `AutomationSubSystem`
self-bootstraps with a no-op guard; Comms/local-speech is wired into `Game.unity` and `Human.prefab`
manually and calls out that a fresh scene/prefab can silently lose that wiring. There are now (at
least) three different "how does a new system get into the running game" answers with no single
place documenting which one a new feature should pick.

- Related: [core-subsystems.md](systems/core-subsystems.md), [scene-management.md](systems/scene-management.md), [chat-audio-screens.md](systems/chat-audio-screens.md)

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

**Blast radius: low-medium — trend: stable**

Ten call sites use `FindObjectOfType`/`FindObjectsByType` for O(n) scene scans. Most are legitimately
Editor-only (catalog builders, debug gizmo drawers) or dev-bypass toggles, which is fine. Three are
not: `TileMap.cs` scans every `Item` in the scene (`FindObjectsOfType<Item>()`), `Locker.cs` scans
every `ContainerViewer`, and — more structurally — `Core/Subsystems.cs`, the service locator every
domain depends on, falls back to `FindObjectOfType` when a subsystem isn't in its registry cache. As
station population and prop density grow, these scans get proportionally more expensive on paths that
were supposed to be O(1) lookups.

- Related: [core-subsystems.md](systems/core-subsystems.md), [tile.md](systems/tile.md)

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
than only growing.)*

- None yet — this doc was created 2026-07-21 as an initial audit; the first item paid down against
  this list should start this section.
