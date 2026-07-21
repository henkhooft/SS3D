> Implements: infrastructure — pays down [TECH_DEBT.md](TECH_DEBT.md) §1.1 and delivers follow-on (d) "Entity prefab setup / recipes" named in [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md)
> Touches systems: entities, health, inventory, combat, core-subsystems
> Status: planned

# Human.prefab decomposition

Ranked #1 structural risk in [TECH_DEBT.md](TECH_DEBT.md) §1.1. This effort turns "strip-and-rewire via
Editor tooling" from a one-off precedent into a repeatable, machine-checked convention, and schedules
the specific extraction that shrinks the prefab today.

## Problem

`Assets/Content/WorldObjects/Entities/Humanoids/Human/Human.prefab` is 15,604 lines with 126 `m_Script`
occurrences (39 distinct component types) spanning health, inventory, movement/animation, combat,
entity identity, interactions, comms, examine, selection, stamina, substances, and networking — one
GameObject graph no agent can safely hand-edit. Auditing it for this effort surfaced four concrete,
previously undocumented problems, not just the general size complaint:

1. **The only precedent tool isn't a Unity tool.** Health's Phase 0d strip-and-rewire (`health.md`,
   `health_implementation_plan.md`) was executed by `Tools/migrate_health_prefabs.py` — a standalone
   Python script doing regex/GUID find-replace on raw prefab YAML outside the Editor, not
   `PrefabUtility`. TECH_DEBT.md's phrase "strip-and-rewire via Editor tooling" describes this
   loosely; there is no reusable C# recipe, contradicting the policy in
   [agent-first-composition.md](2026-07_agent-first-composition.md) that prefab mutation should be
   tool-mediated via `PrefabUtility`/menu items (the pattern already used correctly elsewhere, e.g.
   `Assets/Scripts/SS3D/Systems/Combat/Editor/MeleePrefabSetup.cs`,
   `Assets/Scripts/SS3D/Systems/Inventory/Containers/Editor/StorageContainerPrefabSetup.cs`).
2. **Phase 0d left orphaned missing-script stubs.** 4 of the 39 `m_Script` GUIDs on `Human.prefab` do
   not resolve to any `.cs.meta` in the repo — residual `PrefabInstance` component-override blocks on
   `HumanTorso`/`HumanHandRight`/`HumanFootLeft`/`HumanHead` left over from the migration to
   `AnatomyNode`, despite the health plan's stated acceptance criterion that missing-script cleanup was
   part of Phase 0d.
3. **Docs already point at a tool that was never built.** Both `inventory.md` and `combat.md` cite
   `Assets/Scripts/SS3D/Systems/Inventory/Containers/Editor/BodyPartContainerInteractiveStrip.cs`
   ("SS3D → Inventory → Strip Head/Torso ContainerInteractive") as the mechanism that stripped
   `ContainerInteractive` from `HumanHead`/`HumanTorso`. It does not exist anywhere in the working tree
   or git history — the strip was done by hand, and there is currently no way to safely re-run it if it
   regresses.
4. **Organs are copy-pasted GameObjects, not prefabs.** Unlike the ten body-part prefabs
   (`HumanArmLeft`, `HumanTorso`, `HumanHead`, etc. under `HumanBodyParts/`, correctly wired as
   `PrefabInstance`s), every organ (`HumanBrain`, `HumanHeart`, `HumanLungLeft/Right`, `HumanStomach`,
   `HumanLiver`, `HumanAppendix`, `HumanIntestineLarge/Small` under `HumanOrgans/`) is inlined directly
   into `Human.prefab`'s YAML with no `PrefabInstance` reference, and duplicated again inline in
   `TestHuman.prefab`. Editing an organ means hand-editing the mega-prefab (twice) even though a
   correctly-referenced sibling prefab already exists on disk.
5. A dev-only hack, `Assets/Scripts/SS3D/Hacks/RagdollWhenPressingButton.cs`, is attached directly to
   production `Human.prefab` — the same class of leftover `combat.md` already flags for
   `CombatDummyBootstrap` ("not on Human.prefab, `AddComponent`'d only on spawn").

Enforcement today is convention only: nothing stops a 127th component, another debug hack, or another
copy-pasted organ from landing on `Human.prefab` in review.

## Goals

- A reusable, Unity-native (`PrefabUtility`-based) recipe pattern for evolving `Human.prefab`, so no
  future change requires hand-editing its YAML — this is follow-on (d) from
  [agent-first-composition.md](2026-07_agent-first-composition.md).
- A machine-checked gate (EditMode test in the existing headless CI run) that fails a PR which adds an
  unreviewed component, reintroduces a debug-only hack, or leaves a missing-script reference — closing
  "enforcement is convention only."
- Organs converted to true nested prefabs, matching the pattern already used correctly for body parts.
- The four gaps above fixed as part of the same pass, since they're concrete debt on the exact prefab
  this effort touches, not separate work.

## Non-goals

- Rewriting health/inventory/combat/movement components themselves, or changing runtime behavior.
- Forcing every domain (hands wiring, comms, examine, stamina) to strip-and-rewire in one pass — each
  keeps doing that as part of its own redesign touching entity wiring (existing policy), this effort
  only makes sure a recipe tool exists when they do.
- Body presentation ownership (collapse/death/ragdoll) — that's
  [2026-07_body-presentation-authority.md](2026-07_body-presentation-authority.md), out of scope here
  by that doc's own "Out of scope" note.

## Phased plan

### Phase 0 — Stabilize and instrument (no behavior change)

- Remove the 4 orphaned missing-script override stubs on the `HumanTorso`/`HumanHandRight`/
  `HumanFootLeft`/`HumanHead` `PrefabInstance` blocks in `Human.prefab` (verify `AnatomyNode` already
  covers the behavior before deleting — it should, per health.md).
- Write `BodyPartContainerInteractiveStrip.cs` (`Assets/Scripts/SS3D/Systems/Inventory/Containers/
  Editor/`) as a real `[MenuItem("SS3D/Inventory/Strip Head/Torso ContainerInteractive")]` tool,
  mirroring `StorageContainerPrefabSetup.cs`'s structure, so the two system maps that already cite it
  stop being wrong and the strip becomes safely re-runnable.
- Move or delete `RagdollWhenPressingButton.cs` off production `Human.prefab` (test-only; belongs on a
  `CombatDummyBootstrap`-style spawn-time `AddComponent`, or deleted if superseded by `spawndummy`).
- Add an EditMode test (`Assets/Scripts/Tests/EditMode/`) that loads `Human.prefab` and asserts: no
  `m_Script` GUID fails to resolve to a `.cs.meta`; no component type on an explicit debug/dev
  denylist (starting with anything under `SS3D/Hacks/`) is present. Wire it into
  `.github/workflows/editmodetestrunner.yml` (already runs EditMode headlessly — no new CI plumbing).

### Phase 1 — True prefab-ize the organs

- Write a reusable C# Editor tool (`Assets/Scripts/SS3D/Systems/Health/Editor/OrganPrefabExtract.cs`
  or similar) using `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` /
  `InstantiatePrefab` that: finds each inline organ GameObject on `Human.prefab`, reconciles it against
  the existing (currently orphaned) prefab asset under `HumanOrgans/`, and replaces the inline copy
  with a proper `PrefabInstance` reference. Repeat for `TestHuman.prefab`.
- This tool is the first concrete instance of the Phase 2 recipe convention below, and by itself is the
  single largest line-count reduction available on `Human.prefab` without touching any component logic.

### Phase 2 — Name the recipe convention

- Document (here and in `entities.md`) what a "recipe" is: a `PrefabUtility`-based Editor menu item
  under `SS3D/<Domain>/<Verb>`, one per domain, that attaches or verifies a domain's wiring on an
  entity prefab — generalizing the pattern already used by `MeleePrefabSetup.cs`,
  `StorageContainerPrefabSetup.cs`, and the two tools added in Phases 0–1.
- Add one aggregator menu item (`SS3D/Entities/Run All Prefab Recipes`) that calls every registered
  recipe in sequence, so an agent doesn't need tribal knowledge of which of the 5+ scattered menu items
  exists or needs re-running after a merge.
- No behavior change — this phase is discoverability and naming, not new extraction.

### Phase 3 — Domain strip-and-rewire, scheduled not forced

- Flag inventory's "Human hands wiring remains prefab composition debt" ([inventory.md](systems/inventory.md))
  as the next concrete candidate, since [2026-07_inventory-storage-redesign.md](2026-07_inventory-storage-redesign.md)
  is already in-progress and touches entity wiring — recommend it adopt the Phase 2 recipe convention
  now rather than drift further.
- Every other domain directly on `Human.prefab` (movement/animation, combat, comms, examine, stamina,
  substances) keeps the existing policy: strip-and-rewire happens when that domain's own redesign
  touches entity wiring, using a Phase 2-style recipe tool instead of raw YAML.

## Verification

- After each phase: open `Human.prefab` and `TestHuman.prefab` in the Unity Editor, confirm zero
  missing-script warnings in the Inspector.
- Host Play Mode smoke pass per existing system-map testing notes: movement, health zone hits, hands/
  equip, `spawndummy` (combat), examine, local speech — the same paths already named in
  `health.md`/`combat.md`/`inventory.md`.
- Run the new EditMode test locally via Test Runner, then confirm it passes in
  `editmodetestrunner.yml` CI.
- Track `Human.prefab` line count and distinct `m_Script` type count before/after Phase 1 as the
  quantifiable signal (currently 15,604 lines / 39 types).

## Related docs

- [TECH_DEBT.md](TECH_DEBT.md) §1.1 — ranking and rationale
- [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) — policy this effort
  implements (follow-on d)
- System maps: [entities](systems/entities.md), [health](systems/health.md), [inventory](systems/inventory.md), [combat](systems/combat.md)
- [health_implementation_plan.md](../plans/health_implementation_plan.md) — Phase 0d precedent
