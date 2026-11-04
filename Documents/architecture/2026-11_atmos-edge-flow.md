> Implements: Documents/design/atmospherics.md §2 (open-tile simulation), §5 (fire), §9 (what you see); Documents/design/explosives-destruction.md §4 (breach into atmosphere)
> Touches systems: atmospherics, entities, rendering, networking-session
> Status: in-progress

**Progress:** Phases 0 and 1 done. Phase 0 pinned the fixtures and measured budgets on a sealed map, under both lines on today's code (see [Phase 0 results](#phase-0-results)). Phase 1 put edge flow in `ShareGasJob`, off by default until phase 2 (see [Phase 1 results](#phase-1-results)). Phase 2 is next. Scheduled in M8's slot. The consumer smoothing it builds on shipped in #143. Serves [mvp1-nuke-ops.md](../milestones/mvp1-nuke-ops.md) M8 and must hold [test-server.md](../milestones/test-server.md) T3's 8-client breach line. Tests follow the [testing philosophy](2026-11_testing-philosophy.md).

# Atmos edge flow: pressure-driven wind on the tile grid (Nov 2026)

A hull breach should pull air, bodies and loose objects out of a room within seconds, on the existing tile grid. Today a room leaks slowly because gas only diffuses. This effort gives each open tile edge a flow with momentum, so wind becomes something the simulation computes rather than something inferred from a pressure slope. Zones are rejected: one mixture per room removes the gradient that gives wind a direction, and fire needs per-tile heat (design §5).

## Starting state (2026-11-04, `ab8e396ec` plus the working tree)

- **Transport:** `ShareGasJob` moves `Δn × speed × dt` per edge, about 1 tile²/s of diffusion. Substeps divide `dt`, so they add stability and no speed. Drain time grows with the square of room size.
- **Measured on the real `AtmosSimulation`,** one-tile hole in the middle of a wall of a sealed square room:

| Room | Time to 50% | Time to 10% |
|---|---|---|
| 6×6 | 45 s | 148 s |
| 10×10 | 134 s | 472 s |
| 13×13 | 239 s | over 600 s |

- **A real hole is a neck.** Destruction clears only the wall's own layer, so the plenum stays and the hole is an air tile between the room and space. The table includes that. With the hole tile bare vacuum, as `breach_beside_client` makes it, 10×10 halves in 71 s rather than 134 s, so the neck alone costs a factor of two. Tune Decision 5 against the neck.
- **Prototype:** a Python edge-flow model (wave speed 12 tiles/s, friction 2/s, hole limited to 30 tiles/s) reached 50% in 3 s for 10×10 and 13 s for 20×20. These are a direction, not targets. The prototype has no temperature, composition, doors or sleeping.
- **Consumers are already continuous.** `AtmosWind.SampleAt` blends through open edges, bodies follow the field per frame by drag, and `AtmosWindView` streaks and the gas shaders read the shared flow atlas. Only the source of the field changes here.
- **Chunk borders were one-way valves until this branch** (see the atmospherics map Pitfalls). Connected air spaces now really connect, so a breach can reach further than any earlier measurement implied.

## Model

Each open edge stores a signed flow `Q` in moles per second. The edge between a cell and its east or north neighbour belongs to that cell, so storage is two floats per cell, kept beside the existing buffers.

Per substep, semi-implicit:

1. `Q += dt × (k × (Pa − Pb) − γ × Q)`: pressure accelerates the flow, friction `γ` damps it.
2. Clamp `Q` to the edge's speed limit. Vacuum edges use a lower limit, which stands in for choked flow.
3. Scale each cell's outflows so it cannot lose more than it holds.
4. Move `Q × dt` moles from the upwind cell, carrying that cell's composition and enthalpy. Bulk flow never separates gases.
5. Keep the existing equal-pressure partial-pressure diffusion as a separate path. Breath pockets still need it (design §2).

Explicit stability needs `c × dt < 0.7`, where `c = √k` in tiles per second. At 5 Hz with 4 substeps, `dt` is 0.05 s and `c` must stay under 14 tiles/s.

**Wind** at a cell comes from its four edge flows divided by local gas density, replacing the slope in `AtmosWind.SampleCell`. The flow atlas encodes the same value.

## Decisions

Settled in an implementation grill on 2026-11-04.

| # | Decision |
|---|---|
| 1 | Edge flow replaces bulk transport everywhere, not only during breaches, so opening a door between rooms at different pressures gusts too. A temporary constant keeps the old path until phase 2 passes, then it is deleted. |
| 2 | Edges are processed once each, by their owning cell, so one job writes both ends. This fixes conservation by construction. |
| 3 | The active set stays. A cell sleeps only when its pressure is settled and all its edges are below a flow threshold. A closed door or wall zeroes its edge. |
| 4 | Substeps stay fixed at 4 while any edge exceeds a threshold, and 1 otherwise. A calm station keeps today's cost. |
| 5 | Target feel: a 10×10 room with a one-tile hole reaches half pressure in about 15–20 s. Bigger holes go faster. Design §1's telegraphed catastrophe drove this. |
| 6 | One hole may drain every connected space, with closed doors as the only barrier. No firelocks in this effort (design §12). Reviewed after the playtest. |
| 7 | Knockdown is a wind speed threshold of about 5 m/s, run speed. Below it, wind adds to movement and can be fought. |
| 8 | Wind stays server-authoritative. Owners feel it by `RpcWind`; clients see it through the atlas. No new per-frame traffic. |
| 9 | Fire changes from moving air are accepted. Tests pin design §5's rules, not today's burn numbers. |
| 10 | Budgets during a Metastation breach: at most 4 ms per atmos tick, and at most 300 KB/s per client with 8 clients. Phase 0 measures both. Over on network puts [client sync](2026-07_atmos-client-visualization-sync.md) Phase 2 AOI ahead of phase 1; over on sim reviews the substep rule first. |
| 11 | Loose items are phase 4: anything with a free server `Rigidbody`, drag scaled by mass, with a cap on how many respond at once. Placed furniture stays put. Item physics is already server-only, so no prefab or ownership changes. |

Coasting velocity and fire rendering stay separate follow-ups. They read the same field and do not block this effort.

## Phases

| Phase | Deliverable | Checks |
|---|---|---|
| 0 | Reference fixtures: drain time, far-field onset and wind at distance for 10×10, a 40×8 corridor and a two-room door case. Records the table above as the baseline. Measures Decision 10's budgets on today's code. | EditMode, targeted perf run |
| 1 | Edge flow in `ShareGasJob` behind the constant. Conservation, stability and sleeping. | EditMode: mass and energy conserved in a sealed room, no sign oscillation after a step change, a sealed room settles and sleeps, a door closing zeroes flow, border edges work in both chunk orders |
| 2 | `AtmosWind` and flow encoding read edges. Old path deleted. Fire and breath regressions. | EditMode plus `scenarios:Environment` and `scenarios:Atmos` |
| 3 | Tuning and budget: a 10×10 room drains in Decision 5's time; knockdown moves to Decision 7's rule; `SS3D.Atmos.Sim` and patch bandwidth meet Decision 10. | `mppm:breach`, targeted perf run, screenshot review of a live breach |
| 4 | Loose items ride the wind (Decision 11). | EditMode for drag and the cap, `scenarios:Environment`, patch and `NetworkTransform` bandwidth re-measured |

## Phase 0 results

Measured 2026-11-04 on `d2ecb34e1`.

**Reference fixtures** are `AtmosEdgeFlowReferenceTests`. They pin today's diffusion numbers within 5%, so any transport change has to show up there. Phase 3 replaces them with Decision 5's targets rather than widening the band. Wind is today's slope-derived speed, which caps at 7 m/s beside the hole in every case.

| Case | Half pressure | 10% | Far-field onset (1 kPa) | Peak wind, middle / far |
|---|---|---|---|---|
| 10×10 room | 134 s | 472 s | 13 s | 2.2 / 0.7 m/s |
| 40×8 corridor, hole in the end wall | 684 s | over 1,200 s | 240 s | 0.9 / 0.37 m/s |

Two 6×6 rooms, door opening onto the evacuated one: 259 s to equalise within 5 kPa, 5 s until the far wall of the empty room reads 1 kPa.

**Budgets** were measured on `Testing/fixtures/stations/atmostest.json`, an owner-built sealed map: 3,387 air cells in one connected region across 23 chunks. At rest it passes the sleep check: nothing open to vacuum, gas constant at 352,105 mol, 25–39 cells awake. It was run as one host in Editor Play Mode with Burst, safety checks and the jobs debugger on, so absolute times read high. Bandwidth is dirty chunks × 6.4 KB × 5 Hz per client; no real clients were connected, so Decision 10's 8-client line was not run.

| Measure | Idle | Breach, today's code | Whole region awake | Budget |
|---|---|---|---|---|
| Atmos tick, whole `LastTickMilliseconds` | 1.8 ms | 2.3 ms p50, 3.0 ms max | not measured | 4 ms |
| `AtmosSimulation.Tick` alone | not measured | 0.84 ms p50, 1.1 ms max | 4.5 ms p50, 4.9 ms max | 4 ms |
| Active cells | 25–39 | up to 529 | 3,388 | none |
| Dirty chunks per tick | 0.1 | 4 average, 7 max | not measured | about 9 |
| Patch traffic per client | about 3 KB/s | 127 KB/s average, 224 KB/s max | 737 KB/s if all 23 chunks change | 300 KB/s |

**Gate:** today's code is under both lines, so Decision 10's rule does not put AOI ahead of phase 1. That passes only because diffusion is slow and touches few chunks at once. Edge flow is built to change the whole region quickly, and if every chunk of this region changes each tick it costs 737 KB/s, about 2.5× the budget, and the sim sits just over 4 ms before edge flow adds any cost. Expect network to be the line phase 3 fails: 300 KB/s covers about 9 chunks, roughly a 48×48 tile area, so AOI or smaller patches are likely needed by then.

**Metastation is not a valid budget map.** With no breach, 128 air regions holding 2,509 of its 12,779 air cells touch vacuum, and it lost about 65,000 mol in 50 s, costing 11 ms per tick and about 2,050 KB/s at rest. The owner attributes this to exterior areas that spawn with air and to missing assets that leave gaps. With doors shut its largest region is 517 cells across 12 chunks, so Decision 6's reach depends heavily on which doors are open.

## Phase 1 results

Edge flow runs behind `AtmosFluxConstants.EdgeFlowEnabled`, which stays `false`, so the game and the Phase 0 baselines still use diffusion. Tests turn it on per simulation with `AtmosSimulation.UseEdgeFlow`, the part of the switch a constant alone could not give. Phase 2 deletes both. `AtmosEdgeFlowTests` covers phase 1's checks, plus gas ratio during a drain, a 4,000 K cell and breath pockets.

First-pass constants, not tuned: wave speed 12 tiles/s, friction 8/s, vacuum edge limit 5 tiles/s, one-tile hole with the neck.

| Case | Half pressure | 10% | Far-field onset (1 kPa) |
|---|---|---|---|
| 10×10 room | 19.8 s (diffusion 134 s) | 65 s (472 s) | 1.2 s (13 s) |
| 40×8 corridor | 78 s (684 s) | 262 s (over 1,200 s) | 10.6 s (240 s) |

The 10×10 room already meets Decision 5, but wind in these runs is still the slope-derived value, and sim cost and traffic with edge flow on are unmeasured. Both are phase 3 work.

What phase 1 changed from the model above:

- **Wave speed follows temperature.** Local `c` rises with `√T`, and any edge whose `c × dt` would pass 0.7 has its pressure drive scaled down. The same rule makes Decision 4's single calm substep stable: at 0.2 s, `c` is held to 3.5 tiles/s.
- **Friction is 8/s, not the prototype's 2/s.** By calculation, not by a run at 2/s: a room of `L` tiles stops ringing when friction passes `2πc/L`, about 8/s for a 10-tile room, so smaller rooms can still ring briefly.
- **Substeps run while any gap exceeds 0.5 kPa.** At 2 kPa a breach's tail dropped to the calm path, and a 6×6 room took 169 s to sleep rather than 120 s.
- **Thin gas stops diffusing below `MinSimulationPressure`.** Otherwise it crept toward the hole for over 300 s, holding cells awake and chunks dirty. A drained room sleeps with a little gas left; the test only pins its mean below 5 kPa.
- **The awake front still moves one tile per tick,** 5 tiles/s, below the wave speed. It limits far-field onset in big rooms, which is the corridor's 10.6 s. Review it in phase 3.

## Risks

- **Sloshing:** too little friction makes pressure ring between rooms and keeps cells awake. Phase 1's oscillation test catches it.
- **Near-empty cells:** `Q / density` blows up as density approaches zero. Wind must clamp and fade below a floor density.
- **Network:** a chunk patch is about 6 KB, so 20 changing chunks at 5 Hz is roughly 600 KB/s per client while AOI ([client sync](2026-07_atmos-client-visualization-sync.md) Phase 2) is unshipped. Faster breaches dirty more chunks at once, which puts T3 at risk.
- **Reach:** one hole can empty every connected space (Decision 6). If a single C4 charge ruins whole rounds, firelocks need a design pass.
- **Prediction:** if `HumanoidPredictedMovement` is ever enabled on `Human.prefab`, wind must move into its replicate step, or reconcile undoes it.

## Design changes

The owner revised `atmospherics.md` §1, §2 and §6 on 2026-11-04 to allow bulk flow in open rooms, so this effort no longer deviates from the design. Until phase 2 ships, the code lags the revised sections: rooms still drain by diffusion.
