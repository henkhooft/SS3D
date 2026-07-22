> Code paths: Assets/Scripts/SS3D/Data/
> Entry points: AssetDatabase, AssetDatabasesCodeGenerator
> Status: stub
> Verified: b329ad1a — 2026-07-21

# Data / codegen

## Overview

ScriptableObject asset catalogs, codegen writers producing typed references (`Generated/Scenes.cs`, `Items.cs`, etc.), and shared disk I/O helpers under `Data/Management/`.

## Start here

- `Assets/Scripts/SS3D/Data/AssetDatabases/AssetDatabase.cs` — asset database base
- `Assets/Scripts/SS3D/Data/AssetDatabases/AssetDatabasesCodeGenerator.cs` — codegen entry
- `Assets/Scripts/SS3D/Data/Generated/AssetDatabases.cs` — generated database refs
- `Assets/Scripts/SS3D/Data/Management/LocalStorage.cs` — `JsonUtility` file I/O, append JSONL, legacy path helpers

## Extension points

- New asset categories: extend asset database settings and rerun codegen.
- New interaction icons: drop PNGs under `Assets/Art/Graphics/UI/Interactions/InteractionIcons/`, then **SS3D → Interactions → Rebuild Interaction Icon Sprites** (interim — see debt below). Prefer the Addressables group + `LoadAssetsFromAssetGroup` + `GenerateDatabaseCode` path once that pipeline owns sprites end-to-end.

## Architecture smells

1. **One-off Editor rebuild menus keep multiplying.** Feature work often lands a new `MenuItem` that clones/rewrites assets then registers them (`InteractionIconSpriteBuilder`, MI/Main HUD path-catalog rebuild menus — same pattern called out under [ui-shell](ui-shell.md)). Each fixes a local import/codegen gap but accumulates debt: GUID preservation hacks, `Sprite.Create` vs Instantiate pitfalls, asmdef wiring, and “did anyone run the menu?” drift. Target: one shared import → Addressables → `AssetDatabase.LoadAssetsFromAssetGroup` → codegen path; delete per-feature rebuild scripts as categories migrate onto it. Do not add another one-shot importer without updating this smell.
2. **Addressables groups exist but are never loaded async — every configured asset is eager-loaded into RAM.** `AssetDatabase.LoadAssetsFromAssetGroup()` copies each `AddressableAssetGroup` entry's real `Object` reference into a serialized dictionary at editor time; `Assets.Get<T>` is a sync read against those hard references. No `Addressables.LoadAssetAsync`/`Instantiate*` call exists anywhere in `Assets/Scripts`. Same root cause as upstream [RE-SS3D/SS3D#1494](https://github.com/RE-SS3D/SS3D/issues/1494) ("excessive memory usage"), reached via a different route. Migration scoped in [2026-07_addressables-expansion-migration](../2026-07_addressables-expansion-migration.md) — do not extend this database further as if Addressables already provides on-demand loading; it doesn't yet.

## Pitfalls

- **`Sprite.Create` NativeFormat icons go null in AssetDatabase:** InteractionIcons audit failed when Recycle was authored via `Sprite.Create` + `CreateAsset` (empty `RenderDataKey` / unloadable sprite). Clone the texture’s imported sprite (`Object.Instantiate` of the PNG sub-asset) or use Editor-authored NativeFormat sprites — never commit a one-shot `Sprite.Create` rebuild as the source of truth.

## Depends on / Used by

- **Used by:** Most content-loading systems; [persistence](persistence.md) (`EnvelopePersistenceStore`, `RoundHistoryStore`); [interactions-framework](interactions-framework.md) (`InteractionIcons`)

## Related docs

- [INDEX.md](../INDEX.md)
- Related catalog debt: [ui-shell](ui-shell.md) § Future work (shared path-catalog helper)
- [2026-07_addressables-expansion-migration](../2026-07_addressables-expansion-migration.md) — planned async loading migration
