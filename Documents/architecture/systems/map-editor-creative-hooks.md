> Parent map: [tile.md](tile.md)
> Code paths: Assets/Scripts/SS3D/Systems/Tile/MapEditor/, Assets/Scripts/SS3D/Systems/Tile/SpawnPoints/
> Status: partial
> Verified: a7f2b862f — 2026-10-31

# Map Editor — creative mode extension hooks

## Overview

Reserved extension points shipped with map editor v1 for [creative-mode.md](../../design/creative-mode.md) Phase 2. Spawn Placements and Area metadata now have authoring UI; the remaining reserved hooks are unselected.

## Start here

Spawn Placements authoring:

- Catalog: `spawn:job:{RoleName}` / `spawn:antag:{Category}` under Scripting → Spawn Placements
- Registry: `TileSubSystem.SpawnPoints` (`SpawnPointRegistry`); one marker per tile
- Persist: `SpawnPointPersistenceContributor` (`spawn-points` station-template chunk)
- Effort: [2026-07_spawn-point-authoring](../../archive/architecture/2026-07_spawn-point-authoring.md)
- Still stubbed: RandomSpawners, Triggers, Atmospherics Scripting rails

Area metadata: `MapEditorArea.cs` adds admin-checked inspect/apply RPCs and server undo/redo; `UI/MapEditorAreaView.cs` edits the clicked APC-defined room. Names, department tags and explicit access masks persist through the existing area contributor. Room editing is selected through **Scripts → Rooms**. Its palette uses the standard Construct/Select/Dropper/Delete toolbar; New room is a palette entry, with creation and its first stroke grouped in undo. Scripts markers default off and follow entry/exit of the Scripts category. `MapEditorAreaOverlay` renders the synced area membership with the gizmo color palette (one mesh per chunk; Resources shader). Areas is off by default in Layer View Mode, enabled by Scripts → Rooms across its existing toolbar tools and hidden on other tabs/editor close. Room and layer controls reuse `MapEditorView.SwitchRow`. Department mode previews read-only access bits and retains the custom override. Areas also shows UI Toolkit room-name labels anchored on a tile near each room center; names sync through a buffered snapshot and refresh on rename/undo. Painted boundaries, APC-less room creation and reversible line/rectangle strokes are implemented; manual acceptance is pending ([area-authoring](area-authoring.md)). Manual playtest is pending.

## Extension points

| Hook | Location | v1 default | Phase 2 use |
|------|----------|------------|-------------|
| `IMapEditorAuthorizer` | `MapEditor/Authorizers/` | `AdminMapEditorAuthorizer` | Builder role in creative-mode session |
| `MapEditorPlacementMode` | `MapEditorPlacementMode.cs` | `Normal` | `CreativeInstant` skips construction ladder |
| `MapEditorSaveTarget` | `MapEditorSaveTarget.cs` | `LocalTemplate` | `RoundConfigPool` when round-config lands |
| Scripts → Rooms | `MapEditorAreaPainting.cs`, `UI/MapEditorAreaView.cs` | Standard Construct / Select / Dropper / Delete for rooms | Rich region selection / stamps |
| `MapEditorCatalogSo` filter profiles | not yet in code | full catalog | `CreativeStructural` palette subset |
| Spawn Placements | `MapEditor/MapEditorSpawnCatalog.cs`, `Tile/SpawnPoints/` | **shipped** (authoring + save) | Runtime job→point resolution; round-config coverage |

Data contracts for Phase 2 vertical slice (per creative-mode §10):

1. Gamemode pool entry — empty antag categories, Builder job-list override
2. `MapEditorCommandService` with `PlacementMode.CreativeInstant`
3. Save via `IMapEditorPersistence` (`MapEditor/Persistence/MapEditorPersistence.cs`) targeting round-config map pool (blocked on persistence Phase 1b)

## Pitfalls

- **Room form shows blank labels / default white controls:** runtime panels do not inherit the Editor theme; the room form needs an explicit font, text colors and field/input styles in `MapEditor.uss`. Use stacked labels and bounded scrolling; toolbar tools need baked icon references.

## Tests

- `MapEditorAreaViewTests`: room drafts survive toast/undo-state refresh; department defaults and explicit access submit distinct masks; read-only department previews preserve custom overrides; Areas follows tool selection; overlay meshes preserve tile positions and gizmo colors. Compile + EditMode passed (`artifacts/ui/validate-20261031-143332/summary.md`: succeeded; all 2146 tests completed); manual playtest remains pending.

## Related docs

- Known design deltas: [REVIEW_2026-10 — Known delta](../../design/REVIEW_2026-10.md#known-delta-revised-design-vs-code) — rows: area, creative-mode (area fields, per-gamemode fit check, item placement)
- Parent: [tile](tile.md)
- Design (read-only): [creative-mode.md](../../design/creative-mode.md)
