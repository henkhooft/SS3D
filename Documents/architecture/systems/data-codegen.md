> Code paths: Assets/Scripts/SS3D/Data/
> Entry points: AssetDatabase, AssetDatabasesCodeGenerator, AssetProvider
> Status: partial
> Verified: 4b302f923 — 2026-07-23

# Data / codegen

## Overview

ScriptableObject asset catalogs, codegen writers producing typed references (`Generated/Scenes.cs`,
`Items.cs`, etc.), shared disk I/O helpers under `Data/Management/`, and dual-path asset loading
(eager serialized refs vs Addressables async via `AssetProvider`).

## Start here

- `Assets/Scripts/SS3D/Data/AssetDatabases/AssetDatabase.cs` — asset database base (`LoadMode`, `AssetKeys`)
- `Assets/Scripts/SS3D/Data/AssetDatabases/AssetProvider.cs` — ref-counted Addressables load/release
- `Assets/Scripts/SS3D/Data/AssetDatabases/AssetHandle.cs` — disposable handle
- `Assets/Scripts/SS3D/Data/Assets.cs` — `Get` / `GetAsync` / `PreloadAddressableDatabases`
- `Assets/Scripts/SS3D/Data/AssetDatabases/AssetDatabasesCodeGenerator.cs` — codegen entry
- `Assets/Scripts/SS3D/Data/Generated/AssetDatabases.cs` — generated database refs
- `Assets/Scripts/SS3D/Data/Management/LocalStorage.cs` — `JsonUtility` file I/O, append JSONL, legacy path helpers

## Extension points

- New asset categories: extend asset database settings and rerun codegen.
- New interaction icons: drop PNGs under `Assets/Art/Graphics/UI/Interactions/InteractionIcons/`, then **SS3D → Interactions → Rebuild Interaction Icon Sprites**. Output sprites register into the Addressables group; InteractionIcons DB is `AddressablesAsync` (no eager SO dict). Prefer migrating other DBs onto `LoadMode = AddressablesAsync` + `AssetKeys` rather than growing eager dictionaries.

## Architecture smells

1. **One-off Editor rebuild menus keep multiplying.** Feature work often lands a new `MenuItem` that clones/rewrites assets then registers them (`InteractionIconSpriteBuilder`, MI/Main HUD path-catalog rebuild menus — same pattern called out under [ui-shell](ui-shell.md)). Each fixes a local import/codegen gap but accumulates debt: GUID preservation hacks, `Sprite.Create` vs Instantiate pitfalls, asmdef wiring, and “did anyone run the menu?” drift. Target: one shared import → Addressables → codegen path; delete per-feature rebuild scripts as categories migrate onto it. Do not add another one-shot importer without updating this smell.
2. **Addressables async loading is partial.** InteractionIcons uses `AssetDatabaseLoadMode.AddressablesAsync` + `AssetProvider` warm preload; Items/Materials/Sounds/ParticlesEffects/WorldSpaceUI still eager-load via serialized `Assets` dict. No FishNet preload barrier yet (Phase 4). Migration: [2026-07_addressables-expansion-migration](../2026-07_addressables-expansion-migration.md).

## Pitfalls

- **`Sprite.Create` NativeFormat icons go null in AssetDatabase:** InteractionIcons audit failed when Recycle was authored via `Sprite.Create` + `CreateAsset` (empty `RenderDataKey` / unloadable sprite). Clone the texture’s imported sprite (`Object.Instantiate` of the PNG sub-asset) or use Editor-authored NativeFormat sprites — never commit a one-shot `Sprite.Create` rebuild as the source of truth.
- **AddressablesAsync sync Get needs preload:** `Assets.Get` for an async DB reads `AssetProvider` cache only. Call `Assets.PreloadAddressableDatabases()` after `LoadAssetDatabases()` (done in `AssetsInitializationTrigger`) or icons resolve null with a log and no throw.

## Depends on / Used by

- **Used by:** Most content-loading systems; [persistence](persistence.md) (`EnvelopePersistenceStore`, `RoundHistoryStore`); [interactions-framework](interactions-framework.md) (`InteractionIcons`)

## Related docs

- [INDEX.md](../INDEX.md)
- Related catalog debt: [ui-shell](ui-shell.md) § Future work (shared path-catalog helper)
- [2026-07_addressables-expansion-migration](../2026-07_addressables-expansion-migration.md) — Phases 1–3 done; 4–6 open
