> Parent map: [ui-shell.md](ui-shell.md)
> Status: partial
> Verified: ce8e651c2 — 2026-11-02

# Main menu visuals

## Overview

Unwired UI Toolkit presentation of the owner's **SS3D Main menu design.zip** reference.
No bootstrap, scene changes, session integration, saved identity/recent servers, network queries,
updater invocation, settings writes, URL launches or quit actions. The runtime lobby is unchanged.
The reference's settings placeholder is retained; it does not mount the live settings controls.
Sample addresses, versions, player counts and credentials exist only in the editor preview.

The view covers the 400px sidebar, logo, name field/validation, update banner, advanced port,
footer, recent/public server lists, direct connect/error, server details/admin token, refusal rows,
empty/unavailable public list, and connecting/local-host/update progress dialogs. Operation dialogs
use a cached, reduced-resolution Gaussian wallpaper backdrop. The two supplied wallpapers are
curated presentation assets; random selection and a broader wallpaper pool await integration.

## Start here

- `Assets/Scripts/SS3D/UI/Shell/MainMenu/MainMenuView.cs` — `IUiSurface`, explicit `Attach`/`Detach`, `Present(MainMenuPresentation)`; no action callbacks
- `Assets/Scripts/SS3D/UI/Shell/MainMenu/MainMenuPresentation.cs` — screen, server-status and operation presentation data
- `Assets/Content/Systems/UI/MainMenu/MainMenu.uxml` / `MainMenu.uss` — tree and scoped reference palette
- `Assets/Scripts/SS3D/UI/Shell/MainMenu/MainMenuAssetPaths.cs` / `MainMenuAssetCatalog.cs` — shared catalog pattern; no new document host
- `Assets/Scripts/SS3D/UI/Shell/MainMenu/MainMenuWallpaper.cs` — transient blurred backdrop; restores active render target and releases on detach
- `Assets/Scripts/SS3D/Editor/MainMenuAssetCatalogBuilder.cs` — registered with the existing umbrella catalog rebuild
- `Assets/Scripts/SS3D/Editor/MainMenuPreviewWindow.cs` — **SS3D → UI → Preview Main Menu**, sample-state/resolution/wallpaper controls and `CaptureFrame(path)`
- `Assets/Art/Textures/UI/Wallpapers/` — supplied FloatingSpace2/3; logo/fonts reuse existing Art assets

## Extension points

- Construct with `MainMenuAssetCatalog`, attach explicitly through the UI shell, and supply presentation snapshots.
- Future integration must supply real version/list/name data and action intents; the current view deliberately has no gameplay effects.
- Rebuild assets through **SS3D → Data → Rebuild All UI Catalogs** (`UiCatalogRebuildAll.TryRebuildAll` for automation).

## Pitfalls

- Wallpaper imports must preserve NPOT dimensions. Unity's default power-of-two resize distorted the supplied 1920×1080 artwork; the catalog builder preserves its size and disables compression/mips.
- Gurajada's natural line box is taller than the reference's `line-height: 1`; navigation titles need explicit 20px text boxes to preserve button heights.
- A ScrollView's percentage content min-height did not pin the footer to the viewport. The menu updates the sidebar content's minimum height from its own geometry.
- Editor-window capture must use a linear RenderTexture. An sRGB target double-converts UI colours and produces washed-out reference captures. Screen-pixel readback can return black on Linux; use `CaptureFrame`.

## Tests

- Open **SS3D → UI → Preview Main Menu**. The toolbar selects first/returning launch, update, empty name, advanced port, lists/details, list failures, address/refusal errors, settings and operation states.
- Resolution options reproduce 1920×1080, 1280×720 and 2560×1080 reference frames with aspect-preserving preview scaling.
- `Testing/ui/compile.sh` checks C# and repository analyzers. Gameplay validation is outside this unwired presentation's scope.
- Preview controls select visual fixtures only; they do not establish gameplay, persistence or multiplayer behavior.

## Depends on / Used by

- **Depends on:** [ui-shell](ui-shell.md) catalog/lifecycle contracts and existing Art fonts/logo.
- **Used by:** editor preview only until explicitly integrated.

## Related docs

- [rounds-lobby](rounds-lobby.md) — existing in-session UI/settings, unaffected by this visual implementation
- [agent-first composition](../2026-07_agent-first-composition.md)
