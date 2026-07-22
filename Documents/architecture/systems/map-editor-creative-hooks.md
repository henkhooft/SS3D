# Map Editor — creative mode extension hooks

Reserved extension points shipped with map editor v1 for [creative-mode.md](../../design/creative-mode.md) Phase 2.

| Hook | Location | v1 default | Phase 2 use |
|------|----------|------------|-------------|
| `IMapEditorAuthorizer` | `MapEditor/Authorizers/` | `AdminMapEditorAuthorizer` | Builder role in creative-mode session |
| `MapEditorPlacementMode` | `MapEditorPlacementMode.cs` | `Normal` | `CreativeInstant` skips construction ladder |
| `MapEditorSaveTarget` | `MapEditorSaveTarget.cs` | `LocalTemplate` | `RoundConfigPool` when round-config lands |
| `MapEditorTool.Area` | `MapEditorTool.cs` | unused in UI | Area merge/split/rename tool |
| `MapEditorCatalogSo` filter profiles | future | full catalog | `CreativeStructural` palette subset |
| Spawn Placements | `MapEditor/MapEditorSpawnCatalog.cs`, `Tile/SpawnPoints/` | **shipped** (authoring + save) | Runtime job→point resolution; round-config coverage |

Data contracts for Phase 2 vertical slice (per creative-mode §10):

1. Gamemode pool entry — empty antag categories, Builder job-list override
2. `MapEditorCommandService` with `PlacementMode.CreativeInstant`
3. Save via `IMapEditorPersistence` targeting round-config map pool (blocked on persistence Phase 1b)

## Spawn Placements (shipped authoring)

- Catalog: `spawn:job:{RoleName}` / `spawn:antag:{Category}` under Scripting → Spawn Placements
- Registry: `TileSubSystem.SpawnPoints` (`SpawnPointRegistry`); one marker per tile
- Persist: `SpawnPointPersistenceContributor` (`spawn-points` station-template chunk)
- Effort: [2026-07_spawn-point-authoring](../2026-07_spawn-point-authoring.md)
- Still stubbed: RandomSpawners, Triggers, Atmospherics Scripting rails
