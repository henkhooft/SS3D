> Code paths: Assets/Scripts/SS3D/UI/Shell/
> Entry points: UiShellSubSystem, UiLayer, IUiSurface, UiShellAssetCatalog
> Status: partial
> Verified: f04093919 — 2026-10-31

# UI shell

## Overview

Composition root for player-facing UI Toolkit surfaces. `UiShellSubSystem` self-bootstraps one shared `UIDocument`, builds one child `VisualElement` per `UiLayer` (`Hud`, `Overlay`, `Diegetic`, `Modal`, `Debug`) and applies the global token stylesheets once at the root. Surfaces attach into a layer instead of owning a private document. Policy: [agent-first composition](../2026-07_agent-first-composition.md). Effort: [ui-shell-consolidation](../2026-07_ui-shell-consolidation.md), whose non-goals list the later migration phases.

**On the shell today:**

| Surface | Layer | Owner map |
|---|---|---|
| Radial interaction menu, armed overlay | Overlay | [interactions-runtime](interactions-runtime.md) |
| Examine overlay (hover/detail + character paperdoll) | Overlay | [examine](examine.md) |
| Local-speech chips/compose (`LocalSpeechBubbleController`) | Overlay | [chat-audio-screens](chat-audio-screens.md) |
| Comms feed (`CommsFeedController`) | Hud | [chat-audio-screens](chat-audio-screens.md) |
| Lobby shell + Character Creator (`LobbyUiSubSystem`) | Modal | [rounds-lobby](rounds-lobby.md) |

**Not migrated yet.** Each still has its own `UIDocument` and path catalog, all rebuilt by **SS3D → Data → Rebuild All UI Catalogs**:

| Surface | Paths + SO | Owner map |
|---|---|---|
| Machine UI | `MachineUiAssetPaths` / `MachineUiAssetCatalog` | [machine-interface](machine-interface.md) |
| Main HUD | `MainHudAssetPaths` / `MainHudAssetCatalog` | [inventory](inventory.md) |
| Storage Panel | `StoragePanelAssetPaths` / `StoragePanelAssetCatalog` | [inventory](inventory.md) |

The shell's own catalog (`UiShellAssetPaths` / `UiShellAssetCatalog`) and the lobby's (`LobbyAssetPaths` / `LobbyAssetCatalog`) use the same rebuild. `ExamineOverlayAssetCatalog` is a lighter one-off: no `PanelSettings` or document of its own, not a `UiAssetCatalogBase` derivative.

## Start here

- `Assets/Scripts/SS3D/UI/Shell/UiShellSubSystem.cs` — `TryGetLayer(UiLayer, out VisualElement)` (preferred) / `GetLayer`
- `Assets/Scripts/SS3D/UI/Shell/UiLayer.cs`, `IUiSurface.cs` — layer enum; surface contract implemented by the radial, armed, examine and lobby views
- `Assets/Scripts/SS3D/UI/Shell/Catalog/UiAssetCatalogBase.cs`, `UiCatalogRuntimeLoader.cs` — shared catalog base
- `Assets/Scripts/SS3D/UI/Shell/Animation/PanelAnimator.cs` — shared panel animation
- `Assets/Scripts/SS3D/UI/Shell/Binding/UiBinderBase.cs` — scaffolded binder base with no consumers yet
- Tokens: `Assets/Content/Systems/UI/Tokens/`, `Assets/Content/Systems/UI/MachineInterface/Tokens/`

## Extension points

- New UI is UITK only. Resolve the shell with `SubSystems.Get<UiShellSubSystem>()`, attach via `TryGetLayer`, and implement `IUiSurface`. The effort doc's agent checklist covers the steps.
- A surface that needs its own asset catalog derives from `UiAssetCatalogBase` and registers with `UiCatalogBuilderKit` and the umbrella rebuild menu ([editor-tooling-tiers](../2026-07_editor-tooling-tiers.md)), not a per-surface MenuItem.
- `UiShellSubSystem` creates the document itself, so Boot and Game scenes need no `UIDocument` host.

## Depends on / Used by

- **Depends on:** [inputs](inputs.md) (`InputInterface` document registration)
- **Used by:** every surface in the tables above; [machine-interface](machine-interface.md) and the Main HUD once migrated

## Related docs

- [2026-07_ui-shell-consolidation.md](../2026-07_ui-shell-consolidation.md)
- [2026-07_agent-first-composition.md](../2026-07_agent-first-composition.md)
- [mi-path-catalog](../../archive/architecture/2026-07_mi-path-catalog.md) (archived) — the first path-catalog wedge
- Design (read-only): [main-hud.md](../../design/main-hud.md), [lobby.md](../../design/lobby.md)
