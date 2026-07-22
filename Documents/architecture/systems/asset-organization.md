> Code paths: Assets/Art/, Assets/Content/, Assets/Scripts/, Assets/Editor/, Assets/Settings/, Assets/Resources/
> Entry points: Tools/generate_art_index.py, Tools/generate_icon_index.py
> Status: stub
> Verified: b329ad1a — 2026-07-22

# Asset organization

## Overview

Two top-level trees split raw art from game data/composition: `Assets/Art/` (models, textures, sound,
animations, fonts, icons — organized by asset type then game domain) and `Assets/Content/` (Addressables,
ScriptableObject data, localization, scenes, and per-domain system prefabs/UI under `Content/Systems/`). The
split is sound; adherence has drifted at ~12k files in — see the full audit in
[2026-07_asset-file-structure-taxonomy.md](../2026-07_asset-file-structure-taxonomy.md).

## Start here

Decision table for "where does a new file go" (full version + rationale in the taxonomy doc):

| New file is... | Goes in |
|---|---|
| Any icon image (SVG/PNG) | `Assets/Art/Icons/<Source>/` — always, no exceptions |
| Icon-catalog ScriptableObject (wraps icon assets for code lookup) | Owning system's `Content/Systems/<Domain>/`, named so it never collides with the Art-side icon folder it wraps |
| Cross-system game data (recipes, traits, roles, tilemap defs) | `Content/Data/<Category>/` |
| One-system-only config/catalog | `Content/Systems/<Domain>/` |
| Spawnable entity/furniture/item/structure prefab | `Content/WorldObjects/<Category>/` |
| UI panel / system-internal composition prefab | `Content/Systems/<Domain>/` |
| First-party C# (incl. Editor tooling) | `Assets/Scripts/SS3D/<Area>/` under an `SS3D.*` asmdef |

Also see [AGENTS.md](../../../AGENTS.md) § Finding art assets / Finding UI icons, and the generated indexes
[art-asset-index.md](../../art-asset-index.md) / [icon-index.md](../../icon-index.md).

## Extension points

Adding a genuinely new asset *category* (not covered by the table above): extend the table in the taxonomy
doc, then re-run `generate_art_index.py` / `generate_icon_index.py` so the generated indexes pick it up.

## Pitfalls

- **Icon scatter has already happened once** (8+ locations audited in the taxonomy doc) — don't add a ninth.
  If an icon doesn't fit `Art/Icons/<Source>/`, that's a sign the source taxonomy needs a new bucket, not a
  reason to drop it next to the consuming system.
- **`.meta` pairing on moves:** Unity resolves references by the GUID inside a file's `.meta`, not by path —
  moving an asset and its `.meta` together (e.g. `git mv foo.png foo.png.meta <dest>/`) preserves scene/prefab
  references. String `Resources.Load(path)` calls and Addressables address keys are path-sensitive and are
  **not** covered by this — grep for the old path before any move.
- **`Content/Data/` vs. `Content/Systems/<Domain>/` line:** if more than one system consumes the
  ScriptableObject, it belongs in `Data/`; if it's private config for one UI surface/system, it stays in
  `Systems/`. Getting this wrong is how `Content/Systems/Substances/**` ended up holding domain data that
  belongs in `Data/` (see taxonomy doc Phase 2).

## Depends on / Used by

- Shares its root cause (one-off Editor rebuild menus producing per-feature asset catalogs) with
  [data-codegen.md](data-codegen.md) § Architecture smells.
- Consumed by every domain that adds art, icons, or ScriptableObject data — i.e. all of `Content/Systems/*`.

## Related docs

- [2026-07_asset-file-structure-taxonomy.md](../2026-07_asset-file-structure-taxonomy.md) — full audit, target
  taxonomy, phased migration plan
- [INDEX.md](../INDEX.md)
- [TECH_DEBT.md](../TECH_DEBT.md)
