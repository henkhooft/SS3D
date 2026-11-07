> Implements: Documents/design/creative-mode.md §2 (The palette's layout)
> Touches systems: tile (Map Editor catalog, delete targeting, hologram manager), map-editor-creative-hooks (view, view model, sandbox capabilities, area and gas-marker forms), map validator, tile content folders under `Resources`
> Status: planned

**Progress:** decisions settled in an implementation grill; no phase started. Advances [map-authoring.md](../milestones/map-authoring.md) A8.

# Map Editor palette layout (Nov 2026)

Replaces the palette's sub-tab taxonomy with the one in `creative-mode.md` §2: four tabs (Upper, Lower, Items, Scripts), sub-tabs in build order, objects filed by the layer they sit on, palette-wide search, headings before new tabs, eraser as a tool.

## Starting state (2026-11-07, `db359c98`)

- `MapEditorSubcategory` (`MapEditor/MapEditorMode.cs`) has 21 values, the first 19 implicitly numbered. `MapEditorCatalog.asset` (169 entries, `Assets/Content/Systems/Tile/`) stores them as ints.
- The asset is generated: `Editor/MapEditor/MapEditorCatalogRegenerator.cs` (menu *SS3D/Map Editor/Regenerate Catalog*) rewrites it from `MapEditorCatalogHeuristics`, the only place asset paths are known (runtime has none).
- Classification mixes layer rules (tile objects) with name keywords (items). Furniture holds 47 entries (vendors, SMES, vents, lockers, chairs); Piping holds pipes, inline atmos parts **and** cables; Flooring/Turfs overlap; Catwalk/Lattice fall to Uncategorized; Items › Tools holds materials, parts, the nuke disk and sunglasses.
- Sub-tab facts live in ~8 switches across 15 files: `MapEditorCatalog.GetSubcategories` / `GetSubcategoryLabel` / `IsScriptingSubcategory` / `IsUnavailableScriptingSubcategory`, `MapEditorIconsSo.GetSubcategoryIcon`, `MapEditorDeleteTargeting.Resolve` / `IsItemSubcategory` / `RequiresSubcategorySelection`, `MapEditorViewModel.PlacementHint` / `IsRoomCategory`, `MapEditorView` (sandbox hides Spawn Placements; gas-marker form closes off Atmospherics; Rooms), `ConstructionHologramManager` (delete toast, item delete), `MapEditorSubSystem.TryGetSubcategoryPrototypeAssetName`.
- Delete is scoped to the open sub-tab (`MapEditorDeleteTargeting.Resolve`); Items delete the item under the cursor in any sub-tab.
- `MapValidator` hard-codes tab paths in text ("Add it under Scripts → Spawn Placements").
- `MapEditorCatalog.Query` searches only the open sub-tab, and skips search in Scripts outside Spawn Placements.
- An `IsEraser` catalog entry under Upper › Walls threads through view model, view and subsystem; a Delete tool already exists.
- Nothing remembers the last tab.
- `TileResourceLoader` uses `Resources.LoadAll<GenericObjectSo>("")`: asset paths inside `Resources` don't matter at runtime, so moving assets (with `.meta`) is safe.

## Decisions

Settled with the owner in an implementation grill (2026-11-07). Design premises are `creative-mode.md` §2; nothing here deviates.

1. **Layer-first classification.** Tile objects classify by `TileLayer` + `genericType`; items classify by `Items/` folder, and the folder *is* the sub-tab. Name keywords are deleted.
2. **Furniture-layer split.** A `FurnitureBase`/`FurnitureTop` object is **Machines** unless it sits under `Furniture/Generic`, `Furniture/Flora` or `Furniture/Storage` (→ Furniture). New devices land in Machines without listing.
3. **One static C# sub-tab table** (not a ScriptableObject) replaces the switches. One row per sub-tab: mode, build order, label, icon, delete scope, heading threshold, flags (`IsMarker`, `IsItem`, `SandboxRestricted`). Every former switch reads it.
4. **Fresh enum with explicit values**, old values deleted, never reused. The asset is regenerated in the same commit. An EditMode **drift test** asserts the committed asset equals regeneration output (tier-B pattern, [editor-tooling-tiers](2026-07_editor-tooling-tiers.md)).
5. **Eraser removed completely**: the catalog entry, `MapEditorCatalogEntry.IsEraser`, `CreateEraserEntry`, `IsEraserSelected` and every Edit+eraser path. The Delete tool is the eraser.
6. **Delete scope declared per row, by layer** (table below). Furniture and Machines both clear the furniture layers. Cables getting their own sub-tab retires the `tile.md` pitfall "Piping delete takes pipes before wires".
7. **Couplings by ID or flag, never label:** sandbox hides rows with `SandboxRestricted`; Rooms and Starting Air compare sub-tab IDs; `MapValidator` and the delete toast build text from `PathOf(subTab)` ("Scripts → Spawn Points").
8. **Stubs deleted:** Random Spawners and Triggers lose enum values, icons and the stub message. General rule: a sub-tab whose query is empty is not drawn.
9. **Palette-wide search:** a non-empty search queries all tabs (Scripts included), groups results under "Tab › Sub-tab", and picking a result switches mode and sub-tab before selecting it (delete scope, forms and hints key off the open sub-tab).
10. **Show in palette:** a Map Check issue may carry an entry key; its row gets a button that runs search for that entry.
11. **Last tab remembered** per machine in `PlayerPrefs`, stored by mode and sub-tab **name**; fallback Upper › Floors.
12. **Headings from folders,** baked by the regenerator as a `Heading` string on each entry; drawn only when a sub-tab has ≥ 20 entries (table constant). Machine folders are renamed to the heading words.
13. **Layers popover groups** (`TileLayerCategory`) realign later: Plenums → Subfloor, and "Wires and pipes" splits into Cables / Gas pipes / Disposals.

## Shape

| Tab | Sub-tab (build order) | Classifies | Delete clears |
|---|---|---|---|
| Upper | Floors | Turf, `genericType` Floor | that turf |
| Upper | Walls & Windows | Turf, Wall (incl. windows, girders) | that turf |
| Upper | Doors | Turf, Door | that turf |
| Upper | Wall Mounts | `WallMountLow` / `WallMountHigh` | wall face under cursor |
| Upper | Furniture | furniture layers under `Furniture/Generic`, `/Flora`, `/Storage` | furniture layers |
| Upper | Machines | all other furniture-layer objects | furniture layers |
| Upper | Decals | floor decal catalog | decal |
| Lower | Subfloor | `Plenum` layer (plenum, lattice, catwalk) | plenum |
| Lower | Cables | `Wire` | wire |
| Lower | Gas Pipes | pipe layers incl. `PipeSurface`, `PipeManifold` (inline pumps, valves, filters, mixers, meter, injector, `GasPump`) | pipe layers |
| Lower | Disposal Pipes | `Disposal` | disposal |
| Items | Tools · Materials & Parts · Clothing · Medical · Food & Drink · Devices · Weapons · Misc | `Items/` folder map below | item under cursor |
| Scripts | Spawn Points (`SandboxRestricted`) · Rooms · Starting Air | spawn / area / gas-marker keys | their markers; Rooms unchanged |

**Item folder → sub-tab:** `Clothing` → Clothing; `Consumables/Chemical`, `Functional/Medical` → Medical; `Consumables/Food` → Food & Drink; `Functional/Tools` → Tools; `Functional/Materials`, `Functional/Parts` → Materials & Parts; `Functional/Devices` → Devices; `Weapons` → Weapons; anything else → Misc.

**Asset moves** (within `Assets/Content/Data/TileMap/Resources/`, always with `.meta`):

| Asset | From | To |
|---|---|---|
| NukeCart | `Furniture/Generic` | `Furniture/Machines/General` |
| MachineFrame | `Furniture/Machines` | `Furniture/Machines/General` |
| ScrubberIntake | `Structures/Pipes/Atmospherics` | `Furniture/Machines/Atmos` |
| folder rename | `Furniture/Machines/Engineering`, `/Supply`, `/Atmospherics`, `/Generic` | `Power`, `Disposal`, `Atmos`, `General` |
| Medkit | `Items/Functional/Storage/Objects` | `Items/Functional/Medical` |
| HealthScanner | `Items/Functional/Tools/Medical` | `Items/Functional/Medical` |
| Mug | `Items/Functional/Storage/Substances` | `Items/Consumables/Food/Drinks` |
| ToolboxBlue | `Items/Functional/Storage/Objects` | `Items/Functional/Tools` |
| FloorTile | `Items/Functional/Generic` | `Items/Functional/Materials` |
| Boombox, IDCard, PDA, SecurityIDCard, SecurityPDA | `Items/Functional/Generic/…` | `Items/Functional/Devices` |
| FlashlightBlue | `Items/Functional/Tools/Generic` | `Items/Functional/Devices` |
| NuclearAuthenticationDisk | `Items/Functional/Tools/Command` | `Items/Functional/Devices` |
| Headset, HeadsetSecurity | `Items/Clothing` | `Items/Functional/Devices` |

Confirm each source path in the Editor before moving; any other folder whose contents land in the wrong sub-tab after regeneration is fixed by moving the asset, never by an exception list.

## Phases

### S1 — Reorganisation — **planned**

Atomic: the enum, the asset and the delete switch must change together.

- Sub-tab table + fresh enum; port every switch listed in Starting state to it.
- Rewrite `MapEditorCatalogHeuristics` to decisions 1–2 and the folder map; delete keyword rules.
- Asset and folder moves above; regenerate `MapEditorCatalog.asset`.
- Eraser removal (decision 5); stub removal (decision 8); empty sub-tabs not drawn.
- Delete scope per row (decision 6); couplings (decision 7), including `MapValidator` text and `ConstructionHologramManager` toast.
- Tests: update `MapEditorCatalogMappingTests`, `MapEditorDeleteTargetingTests`, `GasMarkerAuthoringTests`, `MapEditorAreaViewTests`; add the drift test, a per-folder/per-layer classification test, a per-row delete-scope test, and a test that no row's label appears hard-coded in `MapValidator`.
- Docs: `Builds/TESTERS.md`; `tile.md` (remove Piping pitfall, update catalog lines); `map-editor-creative-hooks.md` (Scripts → Rooms, Spawn Points, Starting Air, gas-form pitfall); `area.md`, `area-authoring.md`; `FORK_STATUS.md` where it names tabs.
- Validate: `Testing/ui/validate.sh --steps compile,editmode,scenarios:MapEditor`, plus a manual pass in the editor (every sub-tab non-empty and correct, Delete per sub-tab, sandbox hides Spawn Points).

### S2 — Finding things — **planned**

Palette-wide search with switch-on-pick (decision 9); Map Check **Show in palette** (decision 10); last tab in `PlayerPrefs` (decision 11). Tests: search across tabs, pick switches mode/sub-tab, Scripts searchable, preference round-trip and fallback for an unknown name.

### S3 — Headings — **planned**

`Heading` string on `MapEditorCatalogEntry`, baked by the regenerator from the folder segment below the sub-tab's root; grid renders headings when the sub-tab has ≥ 20 entries. Today only Machines qualifies (Atmos, Power, Disposal, Kitchen, Medical, Vendors, General).

### S4 — Layers groups — **planned**

`TileLayerCategory`: rename Plenums → Subfloor; split Wires and pipes into Cables, Gas pipes, Disposals. Update `MapEditorLayerVisibility` and its tests.

## Parked

- Gas Pipes split into runs and inline parts: only under §2's growth rule (≈ 12 entries and a clear boundary).
- Storage and Atmos Machines as their own sub-tabs: same rule.
- Clothing headings: Clothing has 12 entries after headsets leave, below the threshold; add subfolders when it grows.
