> Implements: infrastructure — no dedicated design doc; would let MI/Main HUD/Storage Panel migrate their catalogs onto [systems/ui-shell.md](systems/ui-shell.md)'s shipped `UiAssetCatalogBase` instead of `Resources.Load`, and closes [systems/data-codegen.md](systems/data-codegen.md) § Architecture smells #1
> Touches systems: data-codegen, ui-shell, networking-session, machine-interface, inventory
> Status: planned

# Addressables expansion & migration

Scopes turning this fork's already-configured but functionally inert Addressables setup into the
thing it's supposed to be: on-demand async asset loading with ref-counting and unload, instead of
every configured asset being hard-referenced into RAM at startup.

## Why

Upstream tracks this exact problem as
[RE-SS3D/SS3D#1494](https://github.com/RE-SS3D/SS3D/issues/1494) ("Excessive memory usage" — up to
600MB from `AssetDatabase.Assets` directly referencing everything from the start) and has an open,
unreviewed 249-commit rewrite attempting to fix it:
[RE-SS3D/SS3D#1500](https://github.com/RE-SS3D/SS3D/pull/1500) ("Make adressables great again").

This fork has the identical root cause today, independently of upstream's PR. Investigated
2026-07-21:

## Current state (this fork)

- **Addressables package is present and configured.** `com.unity.addressables` 2.9.1 (Unity 6),
  active settings at `Assets/Content/Addressables/AddressableAssetSettings.asset`, with **21
  configured groups** (Items, Materials, Sounds, ParticlesEffects, InteractionIcons,
  CraftingRecipes, UIElements, Settings, Scenes, Built In Data, Default Local Group, plus the
  Localization package's auto-managed locale/table groups).
- **But nothing loads through Addressables at runtime.** Grepped `Assets/Scripts` for
  `Addressables.Load*` / `Addressables.Instantiate*` / any async Addressables API — zero call
  sites. Groups exist purely as an **editor-time curation source**.
- **What actually happens:** `AssetDatabase.LoadAssetsFromAssetGroup()`
  (`Assets/Scripts/SS3D/Data/AssetDatabases/AssetDatabase.cs:57`) walks an `AddressableAssetGroup`'s
  entries and copies the real `Object` references into a `SerializableDictionary<string, Object>
  Assets` field serialized directly onto the `AssetDatabase` ScriptableObject. Runtime lookup
  (`Assets.Get<T>` in `Assets/Scripts/SS3D/Data/Assets.cs`) is a synchronous dictionary read against
  those already-loaded, hard-referenced objects.
- **Net effect:** every asset in every configured group is eagerly pulled into RAM the moment its
  owning `AssetDatabase` loads, and stays resident for the process lifetime — functionally identical
  to upstream's #1494, just reached via a different (Addressables-group-as-source-list) route
  instead of raw inspector references.
- **Orphaned cruft:** `Assets/AddressableAssetsData/` (Unity's default settings location) still
  exists alongside the real, active settings folder — leftover duplicate from before settings were
  repointed to `Assets/Content/Addressables/`. Should be deleted as part of any Addressables work
  here, not left as a second source of truth.
- **Already-acknowledged debt pointing at this gap:**
  [data-codegen.md](systems/data-codegen.md) § Architecture smells names the target as "one shared
  import → Addressables → `AssetDatabase.LoadAssetsFromAssetGroup` → codegen path" — but that target
  is itself still the eager/sync pattern described above, not true async loading.
  [ui-shell.md](systems/ui-shell.md) has since shipped `UiShellSubSystem` +
  `UiAssetCatalogBase`/`UiCatalogRuntimeLoader` (Phase 0-1 of
  [2026-07_ui-shell-consolidation.md](2026-07_ui-shell-consolidation.md)) with radial/armed migrated
  onto it, but that effort's own scope explicitly excludes Addressables — MI/Main HUD/Storage Panel
  still each own a `Resources.Load`-based catalog pending their later migration phases. The shared
  scaffolding an Addressables-backed catalog would build on now already exists, which changes Phase
  6 below from "design a shared helper" to "migrate onto the one that shipped." [TECH_DEBT.md](TECH_DEBT.md)
  § 1.5 tracks the Editor-rebuild-menu symptom of the same underlying gap.

## What upstream PR #1500 offers (and why not to merge it wholesale)

The PR's shape is a reasonable reference design:

- `AssetRequest<T>` / `AssetHandle<T>` — ref-counted, disposable, async load handles replacing
  direct object references
- `AssetProvider`, `AssetLifecycleTracker`, `InstanceLifetimeTracker` — dedupe concurrent loads,
  track live instances for unload
- `NetworkBarrier` / `PreloadCondition` — FishNet-specific: makes clients preload an asset before a
  networked spawn RPC references it, and gives prefabs deterministic GUID-based network IDs instead
  of relying on FishNet's default prefab-collection index
- Deletes the legacy static `Assets` lookup entirely once migrated

It is **not mergeable as-is**: it targets Unity 2021.3 + upstream's pre-fork `Data/` layout, this
fork has diverged substantially (Unity 6, different codegen, ~641 commits ahead), it's unreviewed
even upstream, and it migrates ~470 call sites/assets in one shot with only automated test coverage
(no confirmed manual multiplayer smoke). Adopt the **design** — async handle + ref-count model, and
critically the network-preload-barrier idea, since this fork is multiplayer-first via FishNet just
like upstream — not the diff.

## Target model

1. **`AssetHandle<T>`-style async load/release** replacing `Assets.Get<T>` — `AssetDatabase.Get<T>`
   becomes `async Task<AssetHandle<T>> GetAsync<T>(id)` (or a Unity-idiomatic
   `Addressables.LoadAssetAsync` wrapper), ref-counted so multiple callers requesting the same asset
   share one load and release triggers `Addressables.Release` only when the count hits zero.
2. **Keep the existing `AssetDatabase`/`Assets` id-lookup surface and codegen** (`DatabaseID` +
   per-asset string id, `Generated/AssetDatabases.cs`) — this is orthogonal to sync-vs-async and
   already works; only the storage/retrieval mechanism underneath changes from a hard-referenced
   dictionary to an Addressables key lookup.
3. **FishNet spawn ordering** — any networked prefab or synced asset reference needs to be resolvable
   client-side before the spawn/RPC that uses it lands, same problem upstream's `NetworkBarrier`
   solves. Needs a concrete design pass against this fork's `NetworkSpawner`/`NetworkObjectsGenerator`
   equivalents (see [networking-session.md](systems/networking-session.md)) rather than assuming
   upstream's mechanism transfers unchanged.
4. **Migrate database-by-database, not all at once** — start with a database that's easy to verify in
   isolation and has no networked-spawn dependency (e.g. `InteractionIcons` — UI-only, no FishNet
   path) before touching anything that gates a networked prefab spawn (`Items`).

## Phases (proposed — sequencing, not yet scheduled)

1. **Cleanup + baseline** — delete orphaned `Assets/AddressableAssetsData/`; confirm build-pipeline
   settings (Scriptable Build Pipeline, per [FORK_STATUS.md](../FORK_STATUS.md) § Unity 6 upgrade)
   are still correct for actual bundle builds (today they've never been exercised for real streaming,
   only for entries-as-metadata).
2. **Async handle infrastructure** — add `AssetHandle<T>` / ref-counted provider layer alongside the
   existing sync `Assets.Get<T>` (both live briefly, not a big-bang cutover).
3. **First migration slice: `InteractionIcons`** — no network-spawn coupling, exercises the async
   path end-to-end (load → display → release) in a low-risk surface. Validates the model before
   touching gameplay-critical databases.
4. **Network-spawn-coupled databases (`Items`, `CraftingRecipes`, `Materials`)** — requires the
   FishNet preload-ordering design from Target model §3 to land first.
5. **Remove sync `Assets.Get<T>` path** once all call sites are migrated; delete the now-redundant
   `SerializableDictionary<string, Object> Assets` field and `LoadAssetsFromAssetGroup` eager-copy
   step from `AssetDatabase.cs`.
6. **Retire the per-surface `Resources.Load` catalogs** ([TECH_DEBT.md](TECH_DEBT.md) § 1.4) — this
   is the concrete mechanism, not a vague fold-in:

   | Surface | Today | After |
   |---|---|---|
   | Machine UI | `MachineUiAssetPaths` consts + committed `MachineUiAssetCatalog` SO, rebuilt via **SS3D → Machine Interface → Rebuild Asset Catalog** (`MachineUiAssetCatalogBuilder.cs`), loaded with `Resources.Load` | `Addressables.LoadAssetAsync<VisualTreeAsset>("mi/<template>")` (or the `AssetHandle<T>` wrapper from §1) against a stable address; no committed catalog SO |
   | Main HUD | `MainHudAssetPaths` + `MainHudAssetCatalog` SO, rebuilt via **SS3D → Main HUD → Rebuild Asset Catalog** (`MainHudAssetCatalogBuilder.cs`) | same pattern, `"mainhud/<template>"` addresses |
   | Storage Panel | `StoragePanelAssetCatalog`, rebuilt via **SS3D → Storage Panel → Rebuild Asset Catalog** (`StoragePanelAssetCatalogBuilder.cs`) | same pattern, `"storagepanel/<template>"` addresses |

   The failure mode this removes: today, forgetting to re-run a rebuild menu after adding/renaming a
   UXML/USS asset leaves the committed catalog SO silently serving a stale reference — nothing catches
   it until Play Mode or a build. With Addressables addressing there is no second catalog to go stale;
   the Addressables group config *is* the source of truth, and a missing/renamed address fails loudly
   (`LoadAssetAsync` throws/logs "key not found") at the point of use instead of silently no-op'ing.
   Migrate MI first (best-understood surface, see [2026-07_mi-path-catalog.md](2026-07_mi-path-catalog.md)),
   then Main HUD, then Storage Panel; delete each `*AssetCatalog` SO + Editor rebuild menu once its
   surface is migrated.

   **What this does *not* remove:** `InteractionIconSpriteBuilder` (**SS3D → Interactions → Rebuild
   Interaction Icon Sprites**) is a content-authoring step — generating `Sprite` assets from source
   PNGs, including the `Sprite.Create`-goes-null workaround documented in
   [data-codegen.md](systems/data-codegen.md) § Pitfalls — not a loading-mechanism problem. That menu
   still needs to exist; only its *output* (the generated sprites) should land in an Addressables
   group loaded async like everything else in this doc, rather than the eager `AssetDatabase`
   dictionary it uses today.

## Open questions / risks

- **Bundle build verification.** Because nothing has ever actually streamed through Addressables
  here, the content-bundle build path (vs. the current "everything ships as a hard reference,
  Addressables entries are inert metadata") is unverified end-to-end. Needs a real build-and-run
  pass, not just Editor Play Mode (which can silently mask load failures via the Editor's
  fast-path asset resolution).
- **Dedicated server implications.** `UNITY_SERVER` builds ([FORK_STATUS.md](../FORK_STATUS.md) §
  Headless dedicated server) skip renderers/audio but still need correct gameplay assets — async
  loading changes startup-order assumptions there too.
- **No test coverage exists yet** for async load failure/retry paths; EditMode tests should cover
  the handle/ref-count logic in isolation the same way `InputArbiterTests` does for input
  arbitration ([2026-07_input-arbitration.md](2026-07_input-arbitration.md) § Tests).

## Explicit non-goals (this doc)

- Porting upstream PR #1500's code directly — design reference only, per above.
- Migrating MI/Main HUD/Storage Panel onto `UiShellSubSystem` — that's
  [2026-07_ui-shell-consolidation.md](2026-07_ui-shell-consolidation.md)'s own later phases; this
  effort only makes their eventual catalog migration land on async Addressables loading instead of
  the current `Resources.Load` pattern once that migration happens.
- Scene/bundle-based Addressables (remote content delivery, CDN hosting) — local-bundle async
  loading only, matching this fork's build-from-source / manual-prerelease distribution model
  ([FORK_STATUS.md](../FORK_STATUS.md)).

## Related docs

- [systems/data-codegen.md](systems/data-codegen.md) § Architecture smells
- [systems/ui-shell.md](systems/ui-shell.md), [2026-07_ui-shell-consolidation.md](2026-07_ui-shell-consolidation.md)
- [systems/networking-session.md](systems/networking-session.md)
- [TECH_DEBT.md](TECH_DEBT.md) § 1.4, § 1.5, § 1.15
- [2026-07_subsystem-bootstrap.md](2026-07_subsystem-bootstrap.md) — sibling effort for TECH_DEBT § 1.7 (self-initializing subsystems); unrelated root cause, do not conflate
- Upstream: [RE-SS3D/SS3D#1494](https://github.com/RE-SS3D/SS3D/issues/1494), [RE-SS3D/SS3D#1500](https://github.com/RE-SS3D/SS3D/pull/1500)
