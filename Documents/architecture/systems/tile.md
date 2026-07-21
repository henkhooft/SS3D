> Code paths: Assets/Scripts/SS3D/Systems/Tile/
> Entry points: TileSubSystem, AdjacencyEngine, ConstructionService, TileQueryService, MapEditorSubSystem
> Status: partial
> Verified: 3fe7aa22f — 2026-07-21

# Tile / construction

## Overview

Server-authoritative tilemap with adjacency-driven mesh visuals, construction placement, and FishNet HashGrid AOI replication. The adjacency engine queues recompute for walls, doors, pipes, cables, disposal, and furniture connectors. Tile identity sync uses a compact ushort asset catalog. Station template save/load delegates to [persistence](persistence.md) (`PersistenceSubSystem`) with legacy flat-JSON fallback. The in-game **Map Editor** (full-screen UI Toolkit) replaces the legacy TileMap Creator for admin map authoring. Floor department corners are Area-driven mesh stripes (not a tile layer); sparse authored stickers use per-chunk `floorDecalIds`. At spawn / `OnStartClient`, tile renderers OR-in `DecalRenderingLayers.ReceiveWorldDecals` so floor blood Decals can target tiles without painting characters.

**Fork deviation from** [construction.md](../../design/construction.md) **§1:** design's core decision is a staged build ladder (Open → Framed → Plated → Sealed), each stage with distinct system effects (occlusion, atmosphere leak, area-boundary status, §2). `ConstructionService.TryPlaceTile` is a single atomic call that places a finished `TileObjectSo` in one step — no ladder-stage enum or partial states exist anywhere in this folder. Status is `partial`, not `shipped`, because of that gap; the staged ladder is scheduled as follow-up work.

**Condemned UI:** TileMap Creator uGUI — do not extend; Map Editor (UI Toolkit) replaces it for admin authoring. Tile simulation is **not** condemned ([agent-first composition](../2026-07_agent-first-composition.md)).

## Start here

- `Assets/Scripts/SS3D/Systems/Tile/TileCoord.cs` — map+grid key; `IEquatable` required for dictionary use without boxing
- `Assets/Scripts/SS3D/Systems/Tile/PlacedObjects/PlacedTileObject.cs` — per-cell tile NetworkBehaviour; stamps `ReceiveWorldDecals` on renderers
- `Assets/Scripts/SS3D/Systems/Tile/TileSubSystem.cs` — subsystem entry point
- `Assets/Scripts/SS3D/Systems/Tile/TileMap.cs` — tilemap data and mutation
- `Assets/Scripts/SS3D/Systems/Tile/Connections/AdjacencyEngine.cs` — queued adjacency recompute
- `Assets/Scripts/SS3D/Systems/Tile/Connections/TileAdjacencyView.cs` — local mesh/direction visuals
- `Assets/Scripts/SS3D/Systems/Tile/ConstructionService.cs` — server-authoritative placement
- `Assets/Scripts/SS3D/Systems/Tile/TileQueryService.cs` — read-only tile queries (`ITileQueryService`)
- `Assets/Scripts/SS3D/Systems/Tile/ITileMutationObserver.cs` — hook for systems reacting to tile changes
- `Assets/Scripts/SS3D/Systems/Tile/IDynamicTileOccupant.cs` — runtime open/closed state (doors) for occupancy recompute
- `Assets/Scripts/SS3D/Systems/Tile/TileOccupancyEvaluator.cs` — derives passability/vision flags from placed occupants
- `Assets/Scripts/SS3D/Systems/Tile/TileChunk.cs` — per-tile `areaIds` and sparse `floorDecalIds`
- `Assets/Scripts/SS3D/Systems/Tile/FloorVisuals/` — floor decal catalog, mesh helpers, `FloorDecalView`
- `Assets/Scripts/SS3D/Systems/Tile/TileAssetCatalog.cs` — compact tile identity catalog
- `Assets/Scripts/SS3D/Systems/Tile/SingleTileLocation.cs` / `CardinalTileLocation.cs` — per-cell occupancy; `GetAllPlacedObject()` allocates a new `List`
- `Assets/Scripts/SS3D/Systems/Tile/BuildChecker.cs` / `BuildFailReason.cs` — placement rules + toastable fail reasons
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/MapEditorDeleteTargeting.cs` — subcategory → clear targets (wall-mount face via `Direction`)
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/MapEditorSubSystem.cs` — full-screen map editor (admin-gated)
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/UI/MapEditorView.cs` — UI Toolkit editor chrome
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/MapEditorCatalog.cs` — object library taxonomy
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/Commands/MapEditorCommandService.cs` — server undo/redo command layer
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/Commands/MapEditorCommandFactory.cs` — DTO → invertible command (snapshots previous state)
- `Assets/Scripts/SS3D/Systems/Tile/TileMapCreator/ConstructionHologramManager.cs` — placement preview and drag batches
- `Assets/Scripts/SS3D/Systems/Tile/TileMapCreator/TileLayerVisibilityService.cs` — client-only layer-group dim/restore (~5% opacity)
- `Assets/Scripts/SS3D/Systems/Tile/TileMapCreator/TileLayerCategory.cs` — shared layer → category mapping (`FloorDecals` is catalog-backed, not a `TileLayer`)

## Extension points

- New tile objects: create `TileObjectSo` assets and adjacency connectors implementing `IAdjacencyConnector`.
- React to placement: implement `ITileMutationObserver` (see [electricity](electricity.md), [area](area.md), [atmospherics](atmospherics.md)).
- Dynamic passability: implement `IDynamicTileOccupant` and call `TileSubSystem.NotifyTileStateChanged` when state changes (see [furniture](furniture.md) airlocks).
- HV cables (`CablesAdjacencyConnector`): underfloor Wire-layer runs link grid backbone devices only; see [electricity](electricity.md) `ElectricCableConnectivity`.
- Map Editor: `MapEditorSubSystem` (admin-gated via `MapEditorPermissions` / `IMapEditorAuthorizer`). Tools: Select, Edit, Move, Delete, Dropper; toolbar hotkeys **1–4** = Construct / Select / Dropper / Delete (gated while `InputInterface.IsCapturingText`). **Ctrl/Cmd+Shift+O** opens Map Selection (plain Ctrl+O is Unity File/Open Scene); **Ctrl/Cmd+Shift+S** quicksaves (else opens Save Map). Camera pan ignores Ctrl/Cmd/Alt so modifier+S does not also move. Place/delete/decals go through `SubmitCommands` → `MapEditorCommandFactory` → `ExecuteCompound` (one undo step per drag). Ctrl+Z/Y gated while typing. **Phase 2 (deferred):** per-builder stacks + concurrent-edit validity ([creative-mode.md](../../design/creative-mode.md) §6). Placement hard-blocked by `BuildChecker` (`BuildFailReason` toasts). Delete/eraser scopes to library subcategory (`MapEditorDeleteTargeting`; wall-mount face = hologram direction). Layer visibility via `MapEditorLayerVisibility` → `TileLayerVisibilityService` (client-only). **Overlays** subcategory places/clears sparse `floorDecalIds` via undoable `SetFloorDecal` commands. Scripting mode rail is UI-only stub in v1. Creative-mode hooks: [map-editor-creative-hooks](map-editor-creative-hooks.md). UI prefab: `Assets/Content/Systems/UI/MapEditor/MapEditorCanvas.prefab`. Regenerate catalog: `SS3D → Map Editor → Regenerate Catalog`.
- Station templates: `TileSubSystem.Save` / `Load` / `Load(string)` → `PersistenceSubSystem` (`StationTemplates/`, legacy `Tilemaps/`); server boot also calls `LoadServerMeta`. Unknown/removed tile SO names are skipped on load.

## Pitfalls

- **Map Selection Load/Del did nothing (New Map worked):** `HandleUpdate` called `RefreshMapList()` every frame while the maps/save popover was open, so `PopulateLoadList` destroyed and recreated Load/Del mid-click (pointer-down/up never hit the same element). New Map is built once in `BuildMapsPopover`, so it kept working. Refresh only on popover open and after save/delete.
- **Map Selection empty after Save Map:** `MapEditorLocalPersistence` used to pass `SavePath + "/" + name` into `TileSubSystem.Save`/`Load`, but those APIs already prepend `StationTemplates/` via `PersistenceSubSystem` — files landed in `StationTemplates/StationTemplates/` while the UI listed only `StationTemplates/`. Pass bare template names; `ListMaps` uses `ListStationTemplates()` (includes legacy `Tilemaps/`) and migrates any doubled-path leftovers.
- **Ctrl+S panned the camera / Ctrl+O opened Unity's Open Scene:** map session WASD pan ignores Ctrl/Cmd/Alt. Open-map is **Ctrl/Cmd+Shift+O** (Unity's default shortcut profile is read-only, so temporarily clearing File/Open Scene is not viable). Save/quicksave is **Ctrl/Cmd+Shift+S**.
- **Map editor place used to ignore red holograms:** `skipBuildCheck: true` on `RpcPlaceObject` / place commands let invalid tiles commit anyway. Placement now runs `BuildChecker` (Alt replace still checks; only relaxes layer-occupied). Client skips invalid cells and toasts `BuildFailMessages` / `"Skipped N tiles: …"`. Hover shows the primary reason on the Selected Object hint (toast debounced ~0.5s).
- **Map editor undo stayed empty after place/delete:** hologram path used `RpcPlaceObject` / `RpcClear*` and never pushed `MapEditorCommandService`. Place/delete/decals now `SubmitCommands` → factory snapshots → one `Compound` per gesture. Alt-replace undo restores the previous occupant. Design §6 per-builder validity is Phase 2.
- **Map editor Delete cleared every layer:** `EraseAtPointer` wiped all `PlacedTileObject`s on the tile. Delete/eraser now scopes to the object-library **subcategory** via `MapEditorDeleteTargeting` (pipes ≠ disposals). Wall Attachments delete only the hologram face (R cycles). Overlays clear floor decals; Items mode still raycasts. Uncategorized → toast `"Select a subcategory to delete"`.
- **`Dictionary<TileCoord, T>` / `HashSet<TileCoord>` GC on Mono:** without `IEquatable<TileCoord>` + `GetHashCode`, every lookup boxes via `ValueType.DefaultEquals` (~24 B). Prefer `TryGetPlacedObject` over `GetAllPlacedObject` on hot single-occupancy layers — the latter always allocates a new `List`.
- **Icon generation under `-batchmode -nographics`:** `TileResourceLoader.LoadAssetsWithIcon` and `Item.GenerateIcon` use `RuntimePreviewGenerator` (camera → URP). On NullGfxDevice that throws GraphicsBuffer/Blitter exceptions and poisons multiplayer smoke-test logs. Both paths skip when `Application.isBatchMode` or `GraphicsDeviceType.Null` (dedicated server already skipped via `UNITY_SERVER`).
- **B does nothing / Map Editor missing:** `TileCreator.ToggleMenu` (`<Keyboard>/b`) is handled by `MapEditorSubSystem` on `MapEditorCanvas`, nested under `PlayerCanvas`. Never GUID-swap a nested PrefabInstance to a different prefab (ConstructionMenu → MapEditorCanvas once did this) — orphan `fileID`s leave Missing Prefab / SceneId-0 NetworkObjects, so the toggle listener never runs. Re-nest in the Editor or rewrite the PrefabInstance against the source's current local IDs. Map Editor is full-screen UITK, not a DynamicPanels "Construction" tab.
- **Hologram always tracks world east/west:** `CameraFollow.HandleUpdate` still runs via Coimbra `UpdateEvent` after `enabled = false` and was overwriting `MapEditorSession` orbit every frame. Guard with `isActiveAndEnabled`. Map editor must drive/`GetPointedPosition` from `CameraSubSystem.PlayerCamera` (same instance the session orbits). Structural fix: planned [camera ownership](../2026-07_camera-ownership.md) (dedicated manager / contexts — same smell as pre-arbiter input).
- **Hologram slides while orbiting / WASD skewed:** `MapEditorSession` must (1) orbit a ground focus from screen-center pick, (2) freeze hologram picks while MMB orbiting (`IsOrbiting`) so a moving cursor does not drag the ghost, (3) pan from **yaw-only** basis vectors — never `camera.forward` flattened (steep pitch collapses it and sends WASD sideways).
- **Placement through map-editor chrome:** `IMapEditorHost.MouseOverUI` is a live
  `InputInterface.IsPointerOverInterface()` query with the editor `UIDocument` registered.
  Layout policy: regions/`Selected Object` are `PickingMode.Ignore`; library, mode-rail, popovers,
  and buttons are Position. Always convert mouse → panel with `InputInterface.ScreenToPanel` (Y flip)
  inside that authority — unflipped picks map the upper screen onto the Object Library.
  Camera zoom polls `Mouse.current.scroll` directly (MapEditor context masks Input System Zoom);
  gate it with the same `InputInterface` query so wheel over the Object Library ScrollView does not zoom.
  Search/save TextFields hold `InputTextEntryScope` on focus; session pan gates on
  `InputInterface.IsCapturingText` so WASD does not move the camera while typing.
- **Authoring darkness:** Scene lights cannot fullbright Simple Toon (dark rooms stay black; boosting the sun only blows out already-lit spots). `MapEditorLighting` sets shader global `_SS3DAuthoringFullbright` and `VisionRenderContext.Suppressed` for the session.
- **Placement release canceled over “empty” screen:** `InputInterface` must not use `EventSystem.IsPointerOverGameObject()` with a registered fullscreen UITK document — the panel raycaster hits the whole shell. Use `panel.Pick` (Ignore-aware) + uGUI `GraphicRaycaster` only; see [inputs](inputs.md) Pitfalls.
- **Drag path worse on vertical/diagonal / camera angle:** the bottom Object Library region used `PickingMode.Position` over a full-width band, so hover reported UI whenever the cursor was in the lower third — vertical mouse motion and camera framing into that band froze drag and blocked click-hold place. Regions are `Ignore`; only library/windows/slots/buttons pick. Do not freeze drag picks on `MouseOverUI`.
- **Drag path stutters when moving fast:** was Instantiate/Destroy + `RpcSendCanBuild` per hologram per tile step (GC + network hitch). Pool inactive ghosts; skip per-tile validity refresh while dragging; drive path from `Vector2Int` tiles.
- **Shift+drag rectangle:** `Tile Creator / Square Drag` is Shift (filled array); plain drag stays a Bresenham line. Rebuild when the modifier changes even if the cursor tile does not. Replace-existing is Alt (was Shift).
- **Drag placement stuck / dead clicks:** `ConstructionHologramManager` must resolve LMB up/down *before* the orbit early-out — otherwise releasing while MMB-orbiting leaves `_placePressActive` stuck. Also recover if the button is up but the press flag is still set. Cancel (don't commit) when a gesture ends over UI, and rebuild a single cursor hologram so multi-tile drag ghosts do not linger.
- **Drag holograms stayed green on invalid cells:** path used to skip per-tile `TryPreviewTile` for perf (old `RpcSendCanBuild` hitch). Local preview is cheap — `SyncDragHolograms` recolors each ghost; skip toasts list every distinct fail reason (not only the first occupied cell).
- **Map-editor SVG icons blank (e.g. Select cursor):** Unity VectorImage import does not resolve SVG `fill="currentColor"`. Filled shapes must use a concrete color (e.g. `#d8d8d8`); `-unity-background-image-tint-color` then tints them in USS.
- **Save/Load (or any popover) throws `ArgumentOutOfRangeException` in `StylePropertyReader`:** UITK style apply crashes on Map Editor USS footguns — (`-unity-font-definition: initial`), `left`/`right: calc(...)`, nested `var(--missing, var(--fallback))`, and a **wrong `.uss.meta` importer**. `MapEditor.uss` must use Unity's StyleSheet `ScriptedImporter` (`fileID: 12385`), not `DefaultImporter`, or `@import` tokens / stylesheet rebuilds silently break. Use concrete font URLs, `left: 100%` + `margin-left`, and only defined token vars.

## Depends on / Used by

- **Depends on:** [networking-session](networking-session.md) (FishNet AOI), [permissions](permissions.md) (editor admin checks), [persistence](persistence.md) (station template I/O), [chat-audio-screens](chat-audio-screens.md) (player camera; ownership debt)
- **Used by:** [electricity](electricity.md), [area](area.md), [atmospherics](atmospherics.md), [furniture](furniture.md), [substances](substances.md), [persistence](persistence.md) (tilemap contributor)

## Related docs

- Design (read-only): [Documents/design/area.md](../../design/area.md), [Documents/design/creative-mode.md](../../design/creative-mode.md)
- Architecture effort: [2026-07_map-editor-replacement](../2026-07_map-editor-replacement.md); planned camera manager: [2026-07_camera-ownership](../2026-07_camera-ownership.md)
- System map: [area](area.md)
- Plan: [persistence_architecture_design_2fe61864.plan.md](../../plans/persistence_architecture_design_2fe61864.plan.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- Plan: [map_editor_undo_redo.plan.md](../../plans/map_editor_undo_redo.plan.md)
- Plan: [map_editor_build_delete.plan.md](../../plans/map_editor_build_delete.plan.md)
- Effort: [2026-07_tile-overlay-replacement](../2026-07_tile-overlay-replacement.md)
- Plan: [tile_overlay_replacement.plan.md](../../plans/tile_overlay_replacement.plan.md)
