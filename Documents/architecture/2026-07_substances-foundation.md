> Implements: Documents/design/chemistry.md §2–§6 (reagent record, containers/transfer, reactions, thermodynamics, bad-mix resolution hooks); Documents/design/atmospherics.md §3 (shared substance record fields — data only)
> Touches systems: substances, interactions, examine, inventory, atmospherics (definition contract only)
> Status: planned

# Substances foundation

## Goal

Clean-slate rewrite of the liquid substance runtime: data-driven reagent definitions, networked containers, pour-triggered reaction resolution, and container temperature. Ships the **physical primitives** [`chemistry.md`](../design/chemistry.md) and later atmospherics both need. Does **not** ship chemistry gameplay (bloodstream, delivery tools, analyzer knowledge, chem machines, HUD alerts) — that is a separate MVP2 effort.

## Decisions (locked)

1. **Shared `ReagentDefinition` now.** ScriptableObject carries chemistry §2 fields plus atmospherics §3 additions (molar mass, boiling/freezing points) so liquids and future gases share one record. GasBuffer / phase-buffer integration stays deferred with atmos; fields may sit unused until then.
2. **No container “analyzed” flag.** Mixture composition is always the real world state. Color, approximate volume, temperature band, and physical state are free diegetic facts. Chemical identity is **observer knowledge** (per-mind known `ReagentId` set), granted later by analyzer or labeled sources — interpretation of [`chemistry.md`](../design/chemistry.md) §7, not a world-state bit on the vessel. Knowledge filtering is **out of this effort**; foundation examine shows free properties only (or always shows names in debug builds if needed for testing — must not ship as the player-facing discovery model).
3. **Gas tanks stay on `SubstanceContainer`.** Oxygen/plasma/fuel tanks keep using the new container until a dedicated gas-cylinder type exists. Health O₂ treatment may continue as a stub that does not consume moles until chemistry/gas work lands.
4. **No bloodstream container on Human.** Strip empty `SubstanceContainer` from `Human.prefab` in Phase 0. Blood volume remains `SystemicPools`; chemistry doses register later via `IHealthEffectModifier` ([health plan](../plans/health_implementation_plan.md) Phase 7e).
5. **Sequencing.** Substances foundation **now**; chemistry gameplay deferred to **MVP2** (see Deferred).

## Legacy disposition

Treat current `Assets/Scripts/SS3D/Systems/Substances/` as disposable prototype — **no dual-stack**.

| Salvage | Discard / rewrite |
|---------|-------------------|
| Capacity, lock, proportional transfer math | `SubstanceType` enum |
| Tier 2 `TransferSubstance` as one pour path | `ProcessContainer` (dead, O(n) scan) |
| Pitfalls: `AsReadOnly` GC; server-only `InitialSubstances` seeding | Radial `SubstanceDispenser` as chem UX |
| EditMode test *shape* (capacity / lock / proportional remove) | Millimole-first player UX for liquid-only pass |
| Content assets as color/volume seeds if useful | Drink-recipe set as chemistry baseline |

Known legacy defects that the rewrite must not repeat:

- Contents list not networked (only volume/lock SyncVars).
- Temperature unsynced / never written by reactions / no ambient decay.
- Recipes never fire in play (`ProcessContainer` uncalled).

## Target data contract

Names illustrative; match surrounding C# style when implementing.

```text
ReagentId              // stable string / asset id (not an enum)
ReagentDefinition      // SO: name, category, color, physical state (liquid this pass),
                       // density or volume-per-mole, molarMass, boil/freeze K,
                       // metabolism / OD / effect profile (may be unused until chemistry),
                       // flash-related thresholds as needed by §5–§6
MixtureEntry           // (ReagentId, volumeUnits) — volume is player-facing unit
SubstanceContainer     // capacity, lock, synced entries, synced temperature
RecipeDefinition       // inputs + ratios, optional catalyst, optional Tmin,
                       // yield, thermal delta, near-miss / incompatible metadata
ReactionOutcome        // Success | NearMiss | Incompatible | Idle
HazardKind             // Foam | GasRelease | Ignition | FlashFreeze | SmallBlast
                       // (hooks only this effort — full wiring is chemistry/MVP2)
```

**Units:** milliliters (or design “u”) as the player-facing amount. Keep moles available via definition density/molar mass for tanks and future atmos — do not force chemists to author recipes in mmol.

## Phases

### Phase 0 — Purge + contract

- Delete / stop extending legacy substance runtime types; rewrite under the same folder or a clean namespace cut (prefer one clear `SS3D.Systems.Substances` surface).
- Introduce `ReagentDefinition` + registry on `SubstancesSubSystem` (or successor) via asset list / Addressables-friendly catalog — no `SubstanceType` enum.
- Prefab consumers (mugs, soda, oxygen/plasma/fuel tanks, Human empty container): migrate via **tier-B PrefabUtility recipes**, not hand-edited mega-prefab YAML ([agent-first composition](2026-07_agent-first-composition.md)).
- Strip `SubstanceContainer` from `Human.prefab`.
- Replace or rewrite `SubstanceContainerTests` against the new contract.

### Phase 1 — Networked container + transfer

- Server-authoritative mixture (`SyncList` or equivalent) + capacity + lock + temperature SyncVar.
- Seed `InitialSubstances` on **server only** (`OnStartServer`) — keep the smoke-denylist pitfall documented.
- Pour/transfer API; proportional composition move; capacity clamp.
- Keep Tier 2 armed transfer (`ITargetedInteraction`). Tier 3 combine/drag pour when inventory grammar is ready ([main-hud.md](../design/main-hud.md) §8) — optional follow-on inside this effort if Combine is already reliable (disposal already uses Combine).
- Examine: free properties only (mixed color, approximate volume, qualitative temperature) — no identity discovery.
- EditMode: capacity, lock, proportional transfer, serialization/sync packing if testable without FishNet host.

### Phase 2 — Reaction engine + heat

- On every successful pour into a vessel, run `ReactionResolver`:
  1. Match fixed-ratio recipe (+ optional catalyst / T condition) → consume inputs, add yield, apply thermal delta ([chemistry.md](../design/chemistry.md) §4–§5).
  2. Else near-miss or flagged incompatible → `HazardKind` outcome ([chemistry.md](../design/chemistry.md) §6).
  3. Else idle (stable mixture).
- Index recipes by ingredient set — do not resurrect full-table scans.
- Container temperature: initialize from local turf ambient (black-box atmos read, same pattern as other systems); decay toward ambient over time; reactions write the same value (enables heat chaining without extra machinery).
- Correct exotherm past flash threshold → Ignition hazard hook (even when the recipe succeeded).
- Hazard **hooks**: emit a clear server-side event / API. Minimal or stub consumers allowed this effort; full foam/gas/blast/frost wiring waits for chemistry gameplay + existing atmos/blast/health APIs.
- Content vertical slice: small authored set (e.g. two precursors → one product; one heat-gated recipe; one incompatible pair) — not the full drink tree.

## Explicitly deferred (chemistry / MVP2)

Track as a future effort (suggested name `2026-07_chemistry-gameplay.md` when commissioned):

- Per-mind reagent knowledge + chemical analyzer ([chemistry.md](../design/chemistry.md) §7)
- Bloodstream metabolism + sedation via `IHealthEffectModifier` (§8)
- Syringe / patch / pill / ingest / IV delivery (§9)
- Chem dispenser, grinder, pill press as MI surfaces (§10)
- HUD organ-readout extension + OD/sedation alerts (§11)
- Full §6 hazard consumers (atmos gas inject, structural small blast, AoE foam/frost)
- Powered heater furniture; cryogenics consuming container temperature ([cryogenics.md](../design/cryogenics.md))
- Gas/solid reagent states; IV furniture; botanics/food as reagent sources (§13)

## Milestone fit

- Not on MVP1 critical path ([mvp1-nuke-ops.md](../milestones/mvp1-nuke-ops.md)).
- Foundation may ship as infrastructure **before** MVP2; player-facing chemistry depth stays deferred on [mvp2-station-round.md](../milestones/mvp2-station-round.md) until the chemistry gameplay effort lands.

## Related docs

- Design (read-only): [chemistry.md](../design/chemistry.md), [atmospherics.md](../design/atmospherics.md) §3
- System map: [systems/substances.md](systems/substances.md)
- Health hook (later): [health_implementation_plan.md](../plans/health_implementation_plan.md) Phase 7e; `IHealthEffectModifier`
- Composition policy: [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md)
