> Parent map: [tile.md](tile.md)
> Code paths: Assets/Scripts/SS3D/Systems/Tile/MapEditor/, Assets/Scripts/SS3D/Systems/Tile/SpawnPoints/
> Status: partial
> Verified: a675935e6 — 2026-10-31

# Map Editor — creative mode extension hooks

## Overview

Reserved extension points shipped with map editor v1 for [creative-mode.md](../../design/creative-mode.md) Phase 2. The enum values below exist in code but nothing in v1 selects them.

## Extension points


| Hook | Location | v1 default | Phase 2 use |
|------|----------|------------|-------------|
| `IMapEditorAuthorizer` | `MapEditor/Authorizers/` | `AdminMapEditorAuthorizer` | Builder role in creative-mode session |
| `MapEditorPlacementMode` | `MapEditorPlacementMode.cs` | `Normal` | `CreativeInstant` skips construction ladder |
| `MapEditorSaveTarget` | `MapEditorSaveTarget.cs` | `LocalTemplate` | `RoundConfigPool` when round-config lands |
| `MapEditorTool.Area` | `MapEditorTool.cs` | unused in UI | Area merge/split/rename tool |
| `MapEditorCatalogSo` filter profiles | not yet in code | full catalog | `CreativeStructural` palette subset |
| Spawn Placements | `MapEditor/MapEditorSpawnCatalog.cs`, `Tile/SpawnPoints/` | **shipped** (authoring + save) | Runtime job→point resolution; round-config coverage |

Data contracts for Phase 2 vertical slice (per creative-mode §10):

1. Gamemode pool entry — empty antag categories, Builder job-list override
2. `MapEditorCommandService` with `PlacementMode.CreativeInstant`
3. Save via `IMapEditorPersistence` (`MapEditor/Persistence/MapEditorPersistence.cs`) targeting round-config map pool (blocked on persistence Phase 1b)

## Start here: Spawn Placements (shipped authoring)

- Catalog: `spawn:job:{RoleName}` / `spawn:antag:{Category}` under Scripting → Spawn Placements
- Registry: `TileSubSystem.SpawnPoints` (`SpawnPointRegistry`); one marker per tile
- Persist: `SpawnPointPersistenceContributor` (`spawn-points` station-template chunk)
- Effort: [2026-07_spawn-point-authoring](../../archive/architecture/2026-07_spawn-point-authoring.md)
- Still stubbed: RandomSpawners, Triggers, Atmospherics Scripting rails
