> Implements: Documents/design/creative-mode.md §2 (The palette's layout)
> Touches systems: tile (Map Editor catalog, delete targeting, hologram manager), map-editor-creative-hooks (view, view model, sandbox capabilities, area and gas-marker forms), map validator, tile content folders under `Resources`
> Status: in-progress

**Progress:** S1 done (reorganisation, eraser and stubs removed, per-row delete, drift test); S2 done (palette-wide search, Show in palette, last tab, two-line slot labels, compact Map Check rows); S3 done (folder headings in Furniture and Machines); S4–S6 planned. S6 starts with a grill. Advances [map-authoring.md](../milestones/map-authoring.md) A8.

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

### S1 — Reorganisation — **done**

Atomic: the enum, the asset and the delete switch must change together.

- Sub-tab table + fresh enum; port every switch listed in Starting state to it.
- Rewrite `MapEditorCatalogHeuristics` to decisions 1–2 and the folder map; delete keyword rules.
- Asset and folder moves above; regenerate `MapEditorCatalog.asset`.
- Eraser removal (decision 5); stub removal (decision 8); empty sub-tabs not drawn.
- Delete scope per row (decision 6); couplings (decision 7), including `MapValidator` text and `ConstructionHologramManager` toast.
- Tests: update `MapEditorCatalogMappingTests`, `MapEditorDeleteTargetingTests`, `GasMarkerAuthoringTests`, `MapEditorAreaViewTests`; add the drift test, a per-folder/per-layer classification test, a per-row delete-scope test, and a test that no row's label appears hard-coded in `MapValidator`.
- Docs: `Builds/TESTERS.md`; `tile.md` (remove Piping pitfall, update catalog lines); `map-editor-creative-hooks.md` (Scripts → Rooms, Spawn Points, Starting Air, gas-form pitfall); `area.md`, `area-authoring.md`; `FORK_STATUS.md` where it names tabs.
- Validate: `Testing/ui/validate.sh --steps compile,editmode,scenarios:MapEditor`, plus a manual pass in the editor (every sub-tab non-empty and correct, Delete per sub-tab, sandbox hides Spawn Points).

**As built, and where it differs from the plan above** (owner-approved where noted):

- **AtmosMeter and AtmosInjector are Machines, not Gas Pipes** (owner, 2026-11-07). Both sit on `FurnitureBase`, so layer-first filing makes them Machines and the furniture-layer delete clears them. The Shape table's "meter, injector" under Gas Pipes was wrong about their layer. They stay in `Structures/Pipes/Atmospherics/`, where `AtmosInlineDeviceSetup` creates them; S3 needs a heading for them (folder below the Machines root doesn't exist).
- **SteelGirderUnbolted moved** to `Items/Functional/Materials` (owner, 2026-11-07): an `ItemObjectSo` stored under `Structures/Walls`, which the folder map otherwise files as Misc.
- **Map Check grouped bodies name the path.** `MapValidator` folds per-job and operative issues into the `job-spawns` and `nuclear` checklists, so the per-issue "Add it under …" text never reached Map Check. Both grouped bodies now end with `PathOf(SpawnPoints)`.
- **The committed catalog had never loaded:** its `m_Script` GUID matched no script, so the runtime filed every asset by name-keyword fallback. Regeneration fixed the GUID and also dropped six deleted TileOverlay rows and added the missing M4. The baked asset now drives the palette.
- **Enum values start at 100** per tab (Upper 100s … Scripts 400s), so no old 0–20 value can be read as a new sub-tab.
- **Rooms is always drawn**: it lists areas, not catalog entries, so the "empty sub-tab is not drawn" rule (`MapEditorCatalog.IsShown`) skips it. Sandbox players opening Scripts land on Rooms.
- **Icons** are one baked list keyed by each row's icon path (`MapEditorIconsSo._subTabIcons`), not a field per sub-tab.
- **Tier-B recipes repointed**: `AtmosInlineDeviceSetup` writes ScrubberIntake's tile under `Furniture/Machines/Atmos`; `StructuralIntegrityPrefabSetup` maps renamed tile folders back to the unrenamed `WorldObjects` prefab folders.
- **For S3:** Furniture has 21 entries after S1, so it also passes the 20-entry heading threshold, not only Machines.
- Not machine-checked: Items delete (a raycast under the real cursor). The manual pass covered every other sub-tab's delete in a live session.

### S2 — Finding things — **done**

Palette-wide search with switch-on-pick (decision 9); Map Check **Show in palette** (decision 10); last tab in `PlayerPrefs` (decision 11). Tests: search across tabs, pick switches mode/sub-tab, Scripts searchable, preference round-trip and fallback for an unknown name.

**As built:**

- **Added after the owner's hand check (2026-11-07):** slot labels split asset names into words (`MapEditorCatalog.PaletteLabel`) on two lines, because about 47 names were cut off at one line. Map Check issues no longer open on their own, and a closed issue is one line (title, with the category on the right).
- `MapEditorCatalog.Search` returns `MapEditorSearchGroup`s in tab and build order; the grid heads each with "Upper › Machines" (`map-editor-grid-heading`, which S3 reuses). Marker rails now honour a search term; without one they keep kind order. Rooms lists areas, not entries, so search doesn't reach room names.
- A pick goes through `MapEditorSubSystem.OnPaletteEntryPicked`: select, then `MapEditorViewModel.OpenSubTab` (mode and sub-tab in one step), then the ghost. Only palette clicks switch tabs; the Dropper doesn't.
- **Clicking a mode or sub-tab clears the search** (not in the plan): with search showing every tab, a tab click would otherwise look dead.
- `MapValidationIssue.EntryKey` carries the entry. Set on missing job spawns and the nuclear checklist (first unmet requirement), and on the room warnings for a missing air alarm, scrubber, vent or APC. The button searches `MapEditorCatalog.SearchTermFor(key)` (a job, antagonist or marker name, else the asset name), opens the entry's sub-tab and un-minimises the palette. A job with no palette entry (only Assistant and Security have one) gets a toast. The new field changes the `TargetMapValidation` payload: protocol 31 → 32.
- `MapEditorLastTab` stores `"Mode/SubTab"` enum names under `SS3D.MapEditor.LastTab`. It restores on the session's first open; a stored tab under another mode, an unknown name, a number or a hidden sub-tab falls back to Upper › Floors. `SetCapabilities` also moves a sandbox player off a hidden open sub-tab.
- Tests: `MapEditorPaletteSearchTests`. The pick path itself (a `NetworkBehaviour` handler) is covered through `OpenSubTab`, not end to end.

### S3 — Headings — **done**

`Heading` string on `MapEditorCatalogEntry`, baked by the regenerator from the folder segment below the sub-tab's root; grid renders headings when the sub-tab has ≥ 20 entries.

**As built:**

- **Roots:** `MapEditorCatalogHeuristics.HeadingRoots` names them per sub-tab: Machines → `Furniture/Machines/`; Furniture → its three classifying folders. The folder below a root is the heading (split into words like slot labels); a file directly in a root, or outside every root, is **General**. Other sub-tabs bake an empty heading.
- **Furniture gets headings too** (21 entries). Owner chose "by use" (2026-11-07): Seats, Tables, Plants, Lockers, General. Moves: `Furniture/Generic/Surfaces/Tables` → `Furniture/Generic/Tables`, `Furniture/Flora/PottedPlants` → `Furniture/Flora/Plants` (tile assets only; prefabs and examine strings stay put).
- **AtmosMeter and AtmosInjector tiles moved** to `Furniture/Machines/Atmos`, so they head under Atmos. `AtmosInlineDeviceSetup` now writes furniture-layer device tiles to `MachineTileFolder`.
- `MapEditorCatalog.GroupByHeading` sorts headings by name with General last; Machines reads Atmos, Disposal, Kitchen, Medical, Power, Vendors, General. Search results stay grouped by sub-tab only, without folder headings.
- **Compact view puts headings beside the row** (owner, after the hand check, 2026-11-07): it shows one 115 px slot row, and a heading above would overflow it. There the heading (folder or search group) runs bottom-to-top in a 24 px column left of the group's first row; later rows get an empty column so slots line up. The expanded view keeps headings above.
- **Spawn Points lists Nuclear Operatives only** (owner, 2026-11-07): Traitor and Malfunctioning AI are assigned to someone already aboard, so their markers did nothing. `MapEditorSpawnCatalog.MarkerAntagonists` picks the palette's antagonists; the enum values stay so saved records still read.
- Tests: `HeadingOf` cases, the Machines General set is pinned (`EveryMachine_SitsInAHeadingFolder`), heading order, below-threshold and no-heading sub-tabs; the drift test now compares `Heading`.

### S4 — Layers groups — **planned**

`TileLayerCategory`: rename Plenums → Subfloor; split Wires and pipes into Cables, Gas pipes, Disposals. Update `MapEditorLayerVisibility` and its tests.

### S5 — Saved maps reference objects by ID — **planned**

Added by the owner on 2026-11-07. Nobody but the owner has saved maps yet, so a save-format change costs nothing now and more with every outside map.

**Why:** saved maps store objects by name: `SavedPlacedTileObject.tileObjectSOName` and `SavedPlacedItemObject.itemName`, resolved through `RenamedTileObjects`. A rename therefore needs an alias entry and breaks the map-import tools. Stable IDs already exist: each prefab's `ObjectAssetReference.Id` is its Unity asset GUID (the generated `Data/Generated/Items.cs` lists them), and a rename keeps the GUID.

- Save the ID as the key and keep the name beside it, for readable JSON and for loading old saves. Load by ID, then by name through `RenamedTileObjects`.
- Bump `PersistenceEnvelope.CurrentSchemaVersion` (it is 1), and write the shipped maps under `Builds/Game/Data/` in the new format.
- Update `Tools/map_import/` (it writes names) and `MapImportPipeResolver` / `Ss13TypeMapper` if they emit names into saves.
- Check the wire path: whether placement commands and RPCs send names. Moving them to IDs is a protocol bump.
- Test: the old format loads; a saved and renamed asset still loads. An audit test checks that IDs are unique and that no two assets share a GUID (a deleted and recreated asset gets a new one).
- Not in scope, but to record: examine localization keys are built from the asset's folder path (`ExamineCanonicalKeyGenerator.GetNameKey`), so moving or renaming an asset changes its translation key. Decide whether S5 moves those keys to IDs too, or leaves that to a localization effort.
- After S5, renames are cheap. The owner may then shorten names such as `AtmosPipesSupply` (see S2's labels).

### S6 — Variants — **planned; starts with a grill**

Added by the owner on 2026-11-07. Several entries are variants of one object: `AtmosFilterL`/`R`/`L2`/`R2`, `AtmosMixerL`/`R`, `AtmosManifold3`/`4`, the Blue/Grey office and plastic chairs, `Plant0`–`7`, the `TileDirt` tiles and the soda cans. The palette should show one entry per family and cycle its variants. This is not in `creative-mode.md` §2, so it is recorded here as a decision, not as a change to the design doc.

**Step 1 is a `grilling` session with the owner**, before any code. Questions to settle:

- What makes a family: a folder per family (baked by the regenerator, like S3's headings; matches S1's "move the asset, never a rule"), or an explicit list?
- Does a variant stay its own asset in saved maps (no save change, only the palette), or is a variant a property of one object (a save change, which belongs with S5)?
- The cycle input: the scroll wheel zooms the camera (`MapEditorSession`), so Shift+scroll, `[`/`]`, a key, or arrows on the slot. How does it sit beside R rotate? What does the slot show (a "2/4" badge, the variant's name)?
- Which variant is selected by default, and is it remembered per family?
- How Dropper, search (any variant's name finds the family) and Map Check's Show in palette land on a variant.
- Is mirroring (L/R) a variant, or should mirroring be its own transform beside rotation?

Then write the decisions into this section and split it into implementation steps.

## Parked

- Gas Pipes split into runs and inline parts: only under §2's growth rule (≈ 12 entries and a clear boundary).
- Storage and Atmos Machines as their own sub-tabs: same rule.
- Clothing headings: Clothing has 12 entries after headsets leave, below the threshold; add subfolders when it grows.
