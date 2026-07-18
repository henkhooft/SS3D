> Code paths: Assets/Scripts/SS3D/Systems/Tile/
> Entry points: TileSubSystem, AdjacencyEngine, ConstructionService, TileQueryService, MapEditorSubSystem
> Status: shipped
> Verified: 47f084cd3 — 2026-07-18

# Tile / construction

## Overview

Server-authoritative tilemap with adjacency-driven mesh visuals, construction placement, and FishNet HashGrid AOI replication. The adjacency engine queues recompute for walls, doors, pipes, cables, disposal, and furniture connectors. Tile identity sync uses a compact ushort asset catalog. Station template save/load delegates to [persistence](persistence.md) (`PersistenceSubSystem`) with legacy flat-JSON fallback. The in-game **Map Editor** (full-screen UI Toolkit) replaces the legacy TileMap Creator for admin map authoring. At spawn / `OnStartClient`, tile renderers OR-in `DecalRenderingLayers.ReceiveWorldDecals` so floor blood Decals can target tiles without painting characters.

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
- `Assets/Scripts/SS3D/Systems/Tile/TileChunk.cs` — per-tile area-id array (`ushort[]`)
- `Assets/Scripts/SS3D/Systems/Tile/TileAssetCatalog.cs` — compact tile identity catalog
- `Assets/Scripts/SS3D/Systems/Tile/SingleTileLocation.cs` / `CardinalTileLocation.cs` — per-cell occupancy; `GetAllPlacedObject()` allocates a new `List`
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/MapEditorSubSystem.cs` — full-screen map editor (admin-gated)
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/UI/MapEditorView.cs` — UI Toolkit editor chrome
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/MapEditorCatalog.cs` — object library taxonomy
- `Assets/Scripts/SS3D/Systems/Tile/MapEditor/Commands/MapEditorCommandService.cs` — server undo/redo command layer
- `Assets/Scripts/SS3D/Systems/Tile/TileMapCreator/ConstructionHologramManager.cs` — placement preview and drag batches
- `Assets/Scripts/SS3D/Systems/Tile/TileMapCreator/TileLayerVisibilityService.cs` — client-only layer-group dim/restore (~5% opacity)
- `Assets/Scripts/SS3D/Systems/Tile/TileMapCreator/TileLayerCategory.cs` — shared layer → category mapping

## Extension points

- New tile objects: create `TileObjectSo` assets and adjacency connectors implementing `IAdjacencyConnector`.
- React to placement: implement `ITileMutationObserver` (see [electricity](electricity.md), [area](area.md), [atmospherics](atmospherics.md)).
- Dynamic passability: implement `IDynamicTileOccupant` and call `TileSubSystem.NotifyTileStateChanged` when state changes (see [furniture](furniture.md) airlocks).
- HV cables (`CablesAdjacencyConnector`): underfloor Wire-layer runs link grid backbone devices only; see [electricity](electricity.md) `ElectricCableConnectivity`.
- Map Editor: `MapEditorSubSystem` (admin-gated via `MapEditorPermissions` / `IMapEditorAuthorizer`). Tools: Select, Edit, Move; undo/redo via `MapEditorCommandService`. Layer visibility via `MapEditorLayerVisibility` → `TileLayerVisibilityService` (client-only). Scripting & Placements mode rail is UI-only stub in v1. Creative-mode hooks: [map-editor-creative-hooks](map-editor-creative-hooks.md). UI prefab: `Assets/Content/Systems/UI/MapEditor/MapEditorCanvas.prefab`. Regenerate catalog: `SS3D → Map Editor → Regenerate Catalog`.
- Station templates: `TileSubSystem.Save` / `Load` / `Load(string)` → `PersistenceSubSystem` (`StationTemplates/`, legacy `Tilemaps/`); server boot also calls `LoadServerMeta`.

## Pitfalls

- **`Dictionary<TileCoord, T>` / `HashSet<TileCoord>` GC on Mono:** without `IEquatable<TileCoord>` + `GetHashCode`, every lookup boxes via `ValueType.DefaultEquals` (~24 B). Prefer `TryGetPlacedObject` over `GetAllPlacedObject` on hot single-occupancy layers — the latter always allocates a new `List`.
- **Icon generation under `-batchmode -nographics`:** `TileResourceLoader.LoadAssetsWithIcon` and `Item.GenerateIcon` use `RuntimePreviewGenerator` (camera → URP). On NullGfxDevice that throws GraphicsBuffer/Blitter exceptions and poisons multiplayer smoke-test logs. Both paths skip when `Application.isBatchMode` or `GraphicsDeviceType.Null` (dedicated server already skipped via `UNITY_SERVER`).
- **B does nothing / Map Editor missing:** `TileCreator.ToggleMenu` (`<Keyboard>/b`) is handled by `MapEditorSubSystem` on `MapEditorCanvas`, nested under `PlayerCanvas`. Never GUID-swap a nested PrefabInstance to a different prefab (ConstructionMenu → MapEditorCanvas once did this) — orphan `fileID`s leave Missing Prefab / SceneId-0 NetworkObjects, so the toggle listener never runs. Re-nest in the Editor or rewrite the PrefabInstance against the source's current local IDs. Map Editor is full-screen UITK, not a DynamicPanels "Construction" tab.

## Depends on / Used by

- **Depends on:** [networking-session](networking-session.md) (FishNet AOI), [permissions](permissions.md) (editor admin checks), [persistence](persistence.md) (station template I/O)
- **Used by:** [electricity](electricity.md), [area](area.md), [atmospherics](atmospherics.md), [furniture](furniture.md), [substances](substances.md), [persistence](persistence.md) (tilemap contributor)

## Related docs

- Design (read-only): [Documents/design/area.md](../../design/area.md), [Documents/design/creative-mode.md](../../design/creative-mode.md)
- Architecture effort: [2026-07_map-editor-replacement](../2026-07_map-editor-replacement.md)
- System map: [area](area.md)
- Plan: [persistence_architecture_design_2fe61864.plan.md](../../plans/persistence_architecture_design_2fe61864.plan.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
