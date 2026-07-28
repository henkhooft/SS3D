> Code paths: Assets/Scripts/SS3D/Data/Persistence/, Assets/Scripts/SS3D/Systems/Persistence/
> Entry points: PersistenceSubSystem, IPersistenceContributor, EnvelopePersistenceStore
> Status: partial
> Verified: 290aa4e1c — 2026-07-28 (chunked station template restore)

# Persistence

## Overview

Layered contributor-based disk persistence for station templates and server meta. `PersistenceSubSystem` orchestrates ordered save/load via `IPersistenceContributor` implementations and wraps payloads in versioned `PersistenceEnvelope` files. Station templates split tilemap structure, area metadata, and authored spawn markers; server meta currently covers admin permissions (with legacy `permissions.txt` import) and append-only round history JSONL. Round-config map pools and round snapshots are planned.

## Start here

- `Assets/Scripts/SS3D/Data/Persistence/PersistenceEnvelope.cs` — envelope wrapper and schema version
- `Assets/Scripts/SS3D/Data/Persistence/EnvelopePersistenceStore.cs` — JSON file I/O via `LocalStorage`
- `Assets/Scripts/SS3D/Data/Persistence/PersistencePaths.cs` — `StationTemplates/`, `ServerMeta/`, legacy `Tilemaps/`
- `Assets/Scripts/SS3D/Systems/Persistence/PersistenceSubSystem.cs` — orchestrator, lifecycle events; station restore resets world-readiness epoch and notifies `TileMapLoaded` after contributors + deferred area flood; large templates use `LoadStationTemplateAsync` (time-sliced place + bulk mutation mute)
- `Assets/Scripts/SS3D/Systems/Persistence/TileMapPersistenceContributor.cs` — tilemap + items chunk
- `Assets/Scripts/SS3D/Systems/Persistence/AreaPersistenceContributor.cs` — area metadata chunk
- `Assets/Scripts/SS3D/Systems/Persistence/SpawnPointPersistenceContributor.cs` — spawn markers chunk (`spawn-points`, load order 110)
- `Assets/Scripts/SS3D/Systems/Tile/SpawnPoints/SavedSpawnPointRecord.cs` — spawn DTO payload
- `Assets/Scripts/SS3D/Systems/Persistence/PermissionsPersistenceContributor.cs` — admin permissions chunk
- `Assets/Scripts/SS3D/Systems/Persistence/RoundHistoryStore.cs` — append-only round history JSONL
- `Assets/Scripts/SS3D/Systems/Persistence/LegacyTileMapMigrator.cs` — flat tilemap JSON → envelope
- `Assets/Scripts/SS3D/Systems/Persistence/LegacyPermissionsMigrator.cs` — `permissions.txt` → payload
- `Assets/Scripts/SS3D/Data/Persistence/SavedPermissionsPayload.cs` — permissions envelope DTO
- `Assets/Scripts/Tests/EditMode/PersistenceFrameworkTests.cs` — envelope round-trip, legacy tilemap migration, load order
- `Assets/Scripts/Tests/EditMode/SpawnPointAuthoringTests.cs` — spawn registry / DTO / command invertibility
- `Assets/Scripts/Tests/EditMode/ServerMetaPersistenceTests.cs` — permissions migration and round-history append

## Extension points

- New domain: implement `IPersistenceContributor`, register in `PersistenceSubSystem.RegisterBuiltInContributors()` or call `RegisterContributor` at startup; add a `DeserializePayload` arm for the DTO type.
- Station template save/load: `SaveStationTemplate` / `LoadStationTemplate` / `LoadMostRecentStationTemplate`.
- Server meta: `LoadServerMeta` (owned by `PersistenceSubSystem.OnStart` when server), `SaveServerMeta` (on `UserPermissionsChangedEvent`).
- Round history: `AppendRoundHistory` — hooked from `RoundSubSystem.ProcessEndRound`.
- **Deferred:** `RoundConfigPersistenceContributor` (blocked on round-config), round snapshot contributors (Phase 2), round-start `LoadStationTemplate(mapId)` from config pool.

## Pitfalls

- **Station template restore blocks FishNet if sync:** MetaStation-scale `TileMap.Load` without yields freezes Host. Play Mode / Map Editor use `LoadStationTemplateAsync` → `LoadRoutine` + `BeginBulkMutation`. Sync `LoadStationTemplate` remains for EditMode tests only. Hit 2026-07-28.
- **Empty `Data/Tilemaps` (and no StationTemplates) logs `No station templates found to load`.** Fresh Unity player builds have neither tree; loadable releases must ship legacy fixtures from `Builds/Game/Data/Tilemaps/` next to the binary (see [data-codegen](data-codegen.md) Paths pitfall / [CI develop-release](../2026-07_ci-develop-release-pipeline.md)). Smoke seeds the same path when staging.
- **Contributor registration vs LoadServerMeta:** register built-in contributors in `OnAwake`, not `OnStart`. Hub spawn can run other systems' `OnStartServer` before Unity `Start`; LoadServerMeta itself runs from Persistence `OnStart` after all Awakes, so `PermissionSubSystem` is already registered for restore. Do not call `LoadServerMeta` from Tile or other domains.
- **Missing spawn chunk leaves stale markers:** tilemap restore calls `TileMap.Clear`, which clears `TileSubSystem.SpawnPoints`. Do not remove that clear — templates without `spawn-points` must start empty.
- **Station restore epoch:** `RestoreStationTemplate` calls `WorldReadinessSubSystem.NotifyStationTemplateRestoreBeginning` directly (Persistence is hub-spawned; WorldReadiness is DDOL — do not rely on OnBeforeRestore subscription alone).

## Depends on / Used by

- **Depends on:** [data-codegen](data-codegen.md) (`LocalStorage`), [permissions](permissions.md), [tile](tile.md), [area](area.md), [rounds-lobby](rounds-lobby.md), [gamemodes-roles-traits](gamemodes-roles-traits.md) (`CurrentGamemodeName` for round history)
- **Used by:** [tile](tile.md) (save/load backend), [permissions](permissions.md) (import/export), [rounds-lobby](rounds-lobby.md) (round-end history)

## Related docs

- Plan: [persistence_architecture_design_2fe61864.plan.md](../../plans/persistence_architecture_design_2fe61864.plan.md), [spawn_point_authoring.plan.md](../../plans/spawn_point_authoring.plan.md)
- Effort: [2026-07_spawn-point-authoring](../2026-07_spawn-point-authoring.md)
- Effort: [2026-07_session-world-lifecycle](../2026-07_session-world-lifecycle.md)
- Design (read-only): [persistence-save.md](../../design/persistence-save.md) (the four-layer spec this system implements); [round-config.md](../../design/round-config.md) (blocks map pool contributor); [creative-mode.md](../../design/creative-mode.md) §8–§9
