> Code paths: Assets/Scripts/SS3D/Data/
> Entry points: AssetDatabase, AssetDatabasesCodeGenerator, AssetProvider
> Status: partial
> Verified: f1c9476af — 2026-07-25

# Data / codegen

## Overview

ScriptableObject asset catalogs, codegen writers producing typed references (`Generated/Scenes.cs`,
`Items.cs`, etc.), shared disk I/O helpers under `Data/Management/`, and dual-path asset loading
(eager serialized refs vs Addressables async via `AssetProvider`).

## Start here

- `Assets/Scripts/SS3D/Data/AssetDatabases/AssetDatabase.cs` — asset database base (`LoadMode`, `AssetKeys`)
- `Assets/Scripts/SS3D/Data/AssetDatabases/AssetDatabaseLoadMode.cs` — `EagerSerialized` vs `AddressablesAsync`
- `Assets/Scripts/SS3D/Data/AssetDatabases/AssetProvider.cs` — ref-counted Addressables load/release
- `Assets/Scripts/SS3D/Data/AssetDatabases/AssetHandle.cs` — disposable handle
- `Assets/Scripts/SS3D/Data/AssetDatabases/AddressablesLoadBackend.cs` — production Addressables backend
- `Assets/Scripts/SS3D/Data/Assets.cs` — `Get` / `GetAsync` / `PreloadAddressableDatabases`
- `Assets/Scripts/SS3D/Data/AssetsInitializationTrigger.cs` — loads DBs + async preload on `ApplicationInitializing`
- `Assets/Scripts/SS3D/Data/AssetDatabases/AssetDatabasesCodeGenerator.cs` — codegen entry
- `Assets/Scripts/SS3D/Data/Generated/AssetDatabases.cs` — generated database refs
- `Assets/Scripts/SS3D/Data/Management/LocalStorage.cs` — `JsonUtility` file I/O, append JSONL, legacy path helpers
- `Assets/Scripts/SS3D/Editor/UiCatalogRebuildAll.cs` — **SS3D → Data → Rebuild All UI Catalogs** (MI / Main HUD / UI Shell / Storage Panel)
- Tests: `Assets/Scripts/Tests/EditMode/AssetProviderTests.cs` (ref-count / missing key / concurrent missing / preload cache)
## Extension points

- New asset categories: extend asset database settings and rerun codegen.
- New interaction icons: drop PNGs under `Assets/Art/Graphics/UI/Interactions/InteractionIcons/`, then **SS3D → Interactions → Rebuild Interaction Icon Sprites**. Output sprites register into the Addressables group; InteractionIcons DB is `AddressablesAsync` (no eager SO dict). Prefer migrating other DBs onto `LoadMode = AddressablesAsync` + `AssetKeys` rather than growing eager dictionaries.
- New UI path-catalog surface: extend `UiCatalogBuilderKit` / register in `UiCatalogRebuildAll` — do **not** add another `SS3D/.../Rebuild Asset Catalog` MenuItem ([2026-07_editor-tooling-tiers.md](../2026-07_editor-tooling-tiers.md)).

## Architecture smells

1. **Catalog rebuild still not one shared pipeline.** PrefabUtility one-shot MenuItems were thinned (tier B aggregators / tier C deleted), but UI path catalogs + InteractionIcons still rely on Editor rebuild scripts with GUID hacks and “did anyone run the menu?” drift — same pattern under [ui-shell](ui-shell.md). Target: one shared import → Addressables → codegen path; delete per-feature rebuild scripts as categories migrate onto it. Do not add another one-shot importer without updating this smell.
2. **Addressables async loading is partial.** InteractionIcons uses `AssetDatabaseLoadMode.AddressablesAsync` + `AssetProvider` warm preload; Items/Materials/Sounds/ParticlesEffects/WorldSpaceUI still eager-load via serialized `Assets` dict. No FishNet preload barrier yet (Phase 4). Migration: [2026-07_addressables-expansion-migration](../2026-07_addressables-expansion-migration.md).

## Pitfalls

- **Built players need CWD `Config/` + `Data/Tilemaps/` beside the binary.** `Paths.GetPath` uses process CWD (`BuiltGameFilePath` empty; Editor uses `/Builds/Game`). Unity does not pack those trees — missing them → no `permissions.txt`, host log `No station templates found to load`. CI seeds from tracked `Builds/Game/` in [develop-release](../2026-07_ci-develop-release-pipeline.md); never ship `Data/ServerMeta/`.
- **`Sprite.Create` NativeFormat icons go null in AssetDatabase:** InteractionIcons audit failed when Recycle was authored via `Sprite.Create` + `CreateAsset` (empty `RenderDataKey` / unloadable sprite). Clone the texture’s imported sprite (`Object.Instantiate` of the PNG sub-asset) or use Editor-authored NativeFormat sprites — never commit a one-shot `Sprite.Create` rebuild as the source of truth.
- **AddressablesAsync sync Get needs preload:** `Assets.Get` for an async DB reads `AssetProvider` cache only. Call `Assets.PreloadAddressableDatabases()` after `LoadAssetDatabases()` (done in `AssetsInitializationTrigger`) or icons resolve null with a log and no throw.
- **Missing-key load must not `TrySetException` on an unawaited Loading TCS:** `AssetProvider.AcquireAsync` failure with no concurrent waiters used to `TrySetException` then rethrow — UniTask’s unobserved fault logged `[Exception]` and Unity EditMode `LogAssert` failed a later unrelated test. On failure: `TrySetResult(false)`, clear `Loading`, rethrow; waiters already check `Asset == null`. Await UniTask faults in tests (do not rely on `Assert.ThrowsAsync` alone).
- **EditMode fake-backend delay must not use `UniTask.Delay`:** under `-batchmode` EditMode there is no reliable PlayerLoop; even `DelayType.Realtime` can stall (~100s+) or hit the 180s NUnit timeout (`ConcurrentMissingKeyDoesNotLeakUnobservedException`). Use `Task.Delay` in `AssetProviderTests` fakes.

## Depends on / Used by

- **Used by:** Most content-loading systems; [persistence](persistence.md) (`EnvelopePersistenceStore`, `RoundHistoryStore`); [interactions-framework](interactions-framework.md) (`InteractionIcons`)

## Related docs

- [INDEX.md](../INDEX.md)
- Related catalog debt: [ui-shell](ui-shell.md) § Future work (shared path-catalog helper); [2026-07_editor-tooling-tiers.md](../2026-07_editor-tooling-tiers.md)
- [2026-07_addressables-expansion-migration](../2026-07_addressables-expansion-migration.md) — Phases 1–3 done; 4–6 open
