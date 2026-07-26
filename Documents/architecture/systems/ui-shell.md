> Code paths: Assets/Scripts/SS3D/UI/Shell/ (shared); Assets/Scripts/SS3D/UI/ (per-surface, not yet migrated: MachineInterface, MainHud)
> Entry points: UiShellSubSystem, UiLayer, UiShellAssetCatalog
> Status: partial (Phase 0-1 shipped: scaffolding + radial/armed migration)
> Verified: 2026-07-18

# UI shell

## Overview

Composition root for player-facing UI Toolkit surfaces. Layers: **HUD** (persistent), **overlay** (radial, armed,
examine, reticle), **diegetic/modal** (machine panels), **debug** (console). `UiShellSubSystem` owns one shared
`UIDocument` and builds one child `VisualElement` per `UiLayer`, applying the global token stylesheets once at the
root. Surfaces attach into a layer via `SubSystems.Get<UiShellSubSystem>().GetLayer(UiLayer.X)` instead of each
owning a private document. Policy: [2026-07_agent-first-composition.md](../2026-07_agent-first-composition.md).
Effort doc: [2026-07_ui-shell-consolidation.md](../2026-07_ui-shell-consolidation.md).

**Migrated onto `UiShellSubSystem`:** radial interaction menu, armed overlay (`Assets/Scripts/SS3D/Systems/Interactions/`),
the full examine overlay — generic hover/detail *and* character paperdoll — via `ExamineOverlaySubSystem`
(`Assets/Scripts/SS3D/UI/Examine/` — see [examine](examine.md); this is also the redesign that deleted the
condemned uGUI `ExamineUI`, not just a character-only addition). Its `ExamineOverlayAssetCatalog` is a lighter
one-off catalog (no `PanelSettings`/document of its own, since it attaches into the shared document) rather than
a full `UiAssetCatalogBase` derivative — reconcile the two if more self-bootstrapping overlay-only surfaces show up.

**Not yet migrated (still own their own path-catalog wedge — duplicated pattern):**

| Surface | Paths + SO | Rebuild menu | Map |
|---|---|---|---|
| Machine UI | `MachineUiAssetPaths` / `MachineUiAssetCatalog` | **SS3D → Machine Interface → Rebuild Asset Catalog** | [machine-interface](machine-interface.md), [mi-path-catalog](../2026-07_mi-path-catalog.md) |
| Main HUD | `MainHudAssetPaths` / `MainHudAssetCatalog` | **SS3D → Main HUD → Rebuild Asset Catalog** | [inventory](inventory.md) |

Migrating MI and Main HUD onto `UiShellSubSystem` + `UiAssetCatalogBase` is later, separate work (Main HUD next,
MI last since it's shipped and most load-bearing) — not part of the Phase 0-1 wedge this doc currently reflects.

## Start here

- Policy: [2026-07_agent-first-composition.md](../2026-07_agent-first-composition.md)
- Effort doc: [2026-07_ui-shell-consolidation.md](../2026-07_ui-shell-consolidation.md)
- Shared shell: `Assets/Scripts/SS3D/UI/Shell/UiShellSubSystem.cs`, `UiLayer.cs`
- Shared catalog base: `Assets/Scripts/SS3D/UI/Shell/Catalog/UiAssetCatalogBase.cs`, `UiCatalogRuntimeLoader.cs`
- Shared panel animation: `Assets/Scripts/SS3D/UI/Shell/Animation/PanelAnimator.cs`
- Shared binder base (scaffolded, not yet consumed): `Assets/Scripts/SS3D/UI/Shell/Binding/`
- First wedge (MI path catalog): [2026-07_mi-path-catalog.md](../2026-07_mi-path-catalog.md)
- Interim MI: `Assets/Content/Systems/UI/MachineInterface/Resources/MachineUiAssetCatalog.asset`
- Interim Main HUD: `Assets/Content/Systems/UI/MainHud/Resources/MainHudAssetCatalog.asset`
- Tokens: `Assets/Content/Systems/UI/Tokens/`, `Assets/Content/Systems/UI/MachineInterface/Tokens/`

## Extension points

- New UI: UITK only. Attach into an existing `UiShellSubSystem` layer (see `2026-07_ui-shell-consolidation.md`
  agent checklist) rather than a private `UIDocument`. For a surface that still needs its own asset catalog,
  derive from `UiAssetCatalogBase` and use `UiCatalogBuilderKit` for the Editor rebuild-menu boilerplate instead of
  hand-copying the old MI/Main HUD Paths+SO+Builder stack.
- Do not place new `UIDocument` hosts in Boot/Game scenes; `UiShellSubSystem` self-bootstraps the one document new
  surfaces should attach into.

## Future work

- Migrate Machine Interface, Main HUD, and Storage Panel onto `UiShellSubSystem` + `UiAssetCatalogBase` (each its
  own later phase — see [2026-07_ui-shell-consolidation.md](../2026-07_ui-shell-consolidation.md) non-goals).
- Migrate MI binders onto `UiBinderBase<TViewModel>`.
- Drag/drop abstraction (Storage Panel / Main HUD pointer-capture drag).
- Localization/tooltip wiring (still zero UI Toolkit ↔ `LocalizedTextService` call sites).
- Addressables migration remains explicitly out of scope.

## Depends on / Used by

- **Depends on:** [inputs](inputs.md) (`InputInterface` document registration)
- **Owns:** radial interaction menu, armed overlay, examine overlay (generic hover/detail + character paperdoll)
- **Will own:** [machine-interface](machine-interface.md), main HUD ([inventory](inventory.md)), lobby UI, comms UI, examine overlays, console (as redesigns land)

## Related docs

- [2026-07_agent-first-composition.md](../2026-07_agent-first-composition.md)
- [2026-07_ui-shell-consolidation.md](../2026-07_ui-shell-consolidation.md)
- [2026-07_mi-path-catalog.md](../2026-07_mi-path-catalog.md)
- Design (read-only): [Documents/design/main-hud.md](../../design/main-hud.md)
