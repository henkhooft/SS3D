---
name: Atmos machinery
overview: Named pipe lines, real gas supply and inline pipe devices, so a station's air comes from a working atmos room
todos:
  - id: foundation
    content: "PR 1: keep gas across topology changes, pump curve, pipe rename with loader name table, line-based device attachment, stricter Map Check"
    status: completed
  - id: supply
    content: "PR 2: infinite gas source, starting network fill, small atmos room in classic-ministation through the importer"
    status: pending
  - id: inline-devices
    content: "PR 3: two-network device base on PipeSurface, pumps, valves, manifolds on a new PipeManifold layer, GasPump replaced"
    status: pending
  - id: filters-mixers
    content: "PR 4: filters, mixers, meter, injector, scrubber Toxins category, Claude Design UI for filter and mixer"
    status: pending
  - id: optional-extras
    content: "Optional, if effort allows: Ignitor, ConnectorPort with canisters and Connector pipes, pipe rupture, pipe gas temperature"
    status: pending
  - id: docs-sync
    content: "Each PR: update-system-docs for touched maps; MVP2 S2 milestone row when the atmos room ships"
    status: pending
isProject: false
---

# Atmos machinery

Implements `Documents/design/atmospherics.md` §4, §6 and §7. Serves [mvp2-station-round](../milestones/mvp2-station-round.md) S2; not on the Playtest 1 path. Current behaviour: [atmospherics](../architecture/systems/atmospherics.md).

## Current state

Checked against develop 25802878f on 2026-11-05.

- `GasPipeNetworkRegistry.RebuildAround` discards affected networks and creates empty records, so any pipe edit deletes their gas. No test covers this.
- Pipes start empty. The only inflow is `TransferTurfToNetwork`, through scrubbers and pumps, so vents have nothing to supply.
- `AtmosPortFlow.ComputePumpFlowMoles` scales with the absolute differential, so a pump settles at its inlet pressure. A 10-segment network held 101.3 kPa after 2,000 ticks against a 4,500 kPa target.
- `AtmosDevicePipeResolver` takes the first network on the device tile, then its neighbours, across every layer in order. `classic-ministation` runs L1 and L3 over the same 94 tiles and has no L2, so all 11 vents and 12 scrubbers join L1 and L3 is unused. Scrubbers would feed the vents.
- `GasPump` is furniture on `FurnitureBase` that pulls from its tile into a pipe, floating 6 cm above the L4 line its model belongs on. The shipped templates checked place none.
- `MapValidationService` warns about vents and scrubbers with no pipe, but accepts any pipe on the tile or next to it.
- Scrubbers default to pulling O₂ and N₂. Plasma defaults off while Toxins defaults on and also scrubs plasma. Panic overwrites the filters and Filtering never restores them.

## Decisions

**Delivery:** one PR per todo, in order, each usable alone. The optional phase is decided when PR 4 is done.

**Pipe lines:** rename assets by what attaches to them.

| Layer | Today | New name | Attaches automatically |
|---|---|---|---|
| L1, `PipeLeft` | `AtmosPipesL1` | `AtmosPipesSupply` | Vents |
| L2, `PipeMiddle` | `AtmosPipesL2` | `AtmosPipesMixed` | Vents and scrubbers |
| L3, `PipeRight` | `AtmosPipesL3` | `AtmosPipesWaste` | Scrubbers |
| L4, `PipeSurface` | `AtmosPipesL4` | `AtmosPipesSurface` | Inline devices only |

The `TileLayer` enum keeps its names.

**Saved names:** `TileMap` skips names it can't resolve, so a plain rename would drop pipes from every map, tester maps on the sandbox server included. Add an old-to-new table at the load path; maps save under the new names. Also update `teststation.json`, `classic-ministation.json`, `Tools/map_import/` and the examine strings.

**Device attachment:** a vent tries Supply, then Mixed. A scrubber tries Waste, then Mixed. Both look on their own tile only, which covers all 23 devices in `classic-ministation`. The neighbour fallback and `AtmosDevicePipeResolver_FindsNetworkOnAdjacentTile` go. Map Check uses the same rule and covers inline devices.

**Gas conservation:** merges add moles and blend temperature by heat capacity. Splits divide by segment count. A removed segment releases its share into its tile.

**Pump curve:** full rated flow while inlet ≥ outlet. Above that, flow falls linearly to zero at max differential, then stalls. `stalled` and the gauge keep their meaning.

**Infinite source:** a tile object that holds its turf cell at a fixed mix every tick, with one variant per mix (O₂, N₂, air). Hidden in play, visible in the map editor. An existing `Scrubber` serves as the intake. No save-format change.

**Starting fill:** a tile object on a pipe tile fills that network once at round start, after `RebuildAll`. Hidden like the source, with a line chosen per variant. No save-format change.

**Inline devices:** a shared two-network base on `PipeSurface` with ports from rotation, sitting in a gap in the run. This keeps networks separate without topology changes. Pipes treat a device port as a connection, so they draw as a run rather than an open end.

**Layers:** `Pump`, `PumpVolume` and both valves are L4-only; underfloor lines reach them through manifolds. `FilterL2/R2` go on L4. `FilterL/R` and mixer plates sit on the floor in `PipeSurface` and connect to L1–L3 below. Mixers reuse the plate on L4 until art exists. `PipeSurface` isn't an underfloor layer, so plates stay visible and reachable.

**Plate layer choice:** a plate replaces the pipe tile on its own line. It connects to the line with pipes at its ports and a gap under it, so other lines can pass beneath. If that's ambiguous, Map Check warns and the device stays disconnected.

**Manifolds:** a new `PipeManifold` value appended to `TileLayer`. Pipes run through the tile, and the manifold merges the networks of each layer present: L1–L3 for `Manifold3`, L1–L4 for `Manifold4`.

**Valves:** two-network devices. Closed moves nothing; open equalises fast. Toggling never rebuilds topology, so players can't trigger whole-network rebuilds by flipping one. The cost is a brief equalisation instead of an instant merge.

**Filters:** toggle each gas, listed from `GasRegistry`. Selected gases leave by the side port, the rest continue straight. If either outlet is full the device stalls and shows it on its panel. APC power and a rated flow.

**Mixers:** two-inlet ratio and a target outlet pressure. They stop when either inlet is dry.

**Scrubbers:** add a toxic flag to `GasDefinition`. Scrubbers keep a Toxins category over every flagged gas and lose the Plasma toggle. Defaults become CO₂ + Toxins. Filtering restores them after Panic.

**UI:** filter and mixer get new panels, designed in Claude Design. Volume pump reuses the pump template; digital valve gets a minimal on/off panel; manual valve uses a direct interaction; meter uses examine and its shape key.

## Art

`PipeMachinery.fbx` is re-exported from SS3D-Art `PipeMachinery.blend` at 2e2f826. It adds `PipesManifold3`, `PipesManifold4`, `FilterL2` and `FilterR2`, and takes newer `Ignitor`, `Meter`, `ConnectorPort`, `Pump` and `PumpVolume`. The other 9 meshes are unchanged.

Export the `Models` collection with Bake Space Transform on, `FBX_SCALE_ALL`, -Z forward, Y up, no animation, and mesh names matching object names. Prefabs use these meshes on identity transforms, so without baking they tip over 90°. Compare raw vertices with the old FBX afterwards.

Not yet opened in Unity. On first reimport, confirm `GasPump` keeps its mesh and materials.

Left out: `Vertical` pipes and the 8 pipe colour variants from `AtmosphericPipes.blend`. `Connector` pipes join `ConnectorPort` in the optional phase.

## Deviations

`atmospherics.md` was revised with this plan: §6 gained pipe lines, air supply and pipe devices, and §7 and example C now describe backpressure.

PR 1 adds one thing the plan didn't list: the pump caps a tick's flow at the moles left to reach its target. Rated flow at equal pressure would otherwise overshoot by up to 974 kPa per tick on a one-segment run. `GasPump` stays turf-to-pipe furniture until PR 3, and attaches to Surface on its own tile.

## Implementation notes

- `classic-ministation.json` holds 240 pipe entries over 189 distinct saved placements. It loads as 240 segments in 4 networks: 120 Supply and 120 Waste, one per line on each tile. Read the map by tile and line, not by saved placement.
- The SS13 importer still maps `/scrubbers` to Mixed, not Waste. Imported maps work because vents try Supply first, but a vent on a tile with only a scrubber pipe would join it.
- Keep port ticks allocation-free and cache networks against `TopologyVersion`, as `AtmosPortControllerBase` does.
- Dynamic device state doesn't survive template saves; `SavedPlacedTileObject` holds name, origin and direction only.
- Reimport changes in the main checkout wait for the editor to be free.
- Open: `HullBreach_BlowsLooseItemIntoSpace_WhereItComesToRest` in `scenarios:Environment` failed 3 of 10 runs after PR 1 and 0 of 6 on a2bf3a24a, timing out at "the wind carries the item out through the hole". The test room has no pipes and no causal path was found, so this is neither a confirmed regression nor a confirmed flake. Passing runs take about 10.6 s against a 10 s wait; log the wrench's exit time on both commits to compare.

## Tests

- EditMode: merge, split and removal conservation; pump flow at, below and above inlet pressure, and stall; line attachment with Supply, Waste and Mixed; old names load through the table; plate layer choice including the ambiguous case; filter and mixer stalls.
- Scenario: the `classic-ministation` atmos room raises a room's pressure through vents from the chamber, and scrubbers move CO₂ to the waste line.
- Targeted validation per PR: `compile,editmode` plus the focused steps touched. Add `visual` for PRs 3 and 4.
