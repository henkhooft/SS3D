> Code paths: Assets/Scripts/SS3D/Systems/Examine/, Assets/Scripts/SS3D/UI/Examine/, Assets/Scripts/SS3D/Localization/
> Entry points: ExamineSubSystem, ExamineOverlaySubSystem, ExamineContentResolver
> Status: partial
> Verified: 2026-07-26

# Examine

## Overview

Hover tooltips and shift-hold detailed examine panels, range-gated off the [selection](selection.md) system's current `IExaminable`. Supports text and image panel variants. Localization uses a unified Examine string table and `LocalizedTextService`; dynamic content via `IExamineContentProvider` (e.g. identification cards).

Examine is **not** an `IInteraction` — `ExaminableBase` is read by `ExamineSubSystem` from the current selection (hover + Shift). There is no `ExamineInteraction` petal class on develop.

**Rendering is UI Toolkit** (`ExamineOverlaySubSystem`, `Assets/Scripts/SS3D/UI/Examine/`) — the previous uGUI `ExamineUI`/`ExamineDetailedView`/`ExamineImageDetailedView` trio (and its dead `NetworkSystemsHub.prefab` entry — `HoverName` was never wired, so it rendered nothing) has been deleted, not ported; this was the redesign's Phase 0 purge per [agent-first composition](../2026-07_agent-first-composition.md)'s "replace, don't bridge" rule. One subsystem now owns every examine surface:

- **`GenericExamineHoverView`** — plain-hover name label; Shift-held (or a radial-menu pin via `ExamineSubSystem.OnDetailedExamineRequested`) swaps to a detailed text or image panel, gated by `ExamineRangeUtility`/`RangeLimit` for images exactly as before. This is the direct UITK replacement for the deleted uGUI views — same behavior, same range-gating, new renderer.
- **`CharacterQuickLookView`** / **`CharacterExamineWindowView`** — character-only paperdoll surfaces (see below).

**Fork deviations from** [examine.md](../../design/examine.md): design specifies a single momentary hold-to-peek key with no hover/click-lock state (§1/§4); shipped code always shows the name tooltip on plain hover, with Shift only gating the richer detailed content. Accepted as intentional UX direction, not scheduled for rework.

**Character examine** implements design §7's "examine another character" target type — this closes the gap earlier versions of this doc flagged. It needed **no new component and no `Human.prefab` growth**: the prefab's root already carries `Selectable` + `SimpleExaminable` + `HumanInventory` (characters were already examinable; the old uGUI system just rendered nothing there because `HoverName` was unwired). The only data change was flipping the existing `Human.asset` (`Assets/Content/Data/Examine/String/Entities/Humanoids/Human.asset`) from `Type: SIMPLE_TEXT` to `Type: CHARACTER` — `ExamineOverlaySubSystem` discriminates purely on `examinable.GetData().Type == ExamineType.CHARACTER`, then does `((Component)examinable).GetComponent<HumanInventory>()` to read equipment. **Do not reintroduce a `CharacterExaminable` marker component or a `Human.prefab` Editor recipe for this** — an earlier draft of this feature did exactly that and was reverted; see [TECH_DEBT.md](../TECH_DEBT.md) §1.1, the project's #1-ranked structural risk, which explicitly forbids "add one more behaviour" as a `Human.prefab` feature path.

It is a **second, deliberate departure** from §4's hold-to-peek/no-click-lock discipline, scoped to characters only:
- **Hover** → `CharacterQuickLookView` shows a compact paperdoll preview unconditionally (no Shift needed) — different from every other examinable, which shows only a name on hover.
- **Shift+Click** on a character → `CharacterExamineWindowView`, a *persistent* window (only its × closes it, no auto-close on mouse-leave/re-hover) — the click-lock state §4 explicitly rules out for the general case. Wired in `InteractionController.HandleRunPrimary` (checks `InputSubSystem.DetailedExamine.IsPressed()` + the hovered `IExaminable`'s `Type == CHARACTER`), routed through `ExamineSubSystem.OnCharacterWindowRequested` rather than a direct reference, so the interactions layer never depends on the UI-layer window.
- **Hold-to-take** (~650ms hold on an equipped slot) is a **UI-only stub**: shows the hold/pulse affordance and clears the slot's displayed icon locally, but does **not** perform any networked item transfer — `CharacterExamineWindowView.CompleteHold` is explicitly marked `FOLLOW-UP` for whoever designs the actual loot-another-character permission/range model.
- **Covered/obscured-slot filtering is not implemented** — no existing `Item`/`ClothingItemPresentation` data tracks "hidden by an outer worn layer," so every equipped item currently shows regardless of what's worn over it. Revisit once that data exists.
- Slot↔`ContainerType` mapping (`CharacterExamineSlotContainerMap`) reuses Main HUD's precedent for overlapping slots (Back→Bag, Shirt→Jumpsuit, paired Ears/Feet/Gloves) and gives the previously-unused `ContainerType.ExoSuit` its first real UI surface (mapped from the paperdoll's "Suit" row).

**One-time Editor step before any of this renders:** **SS3D → Examine → Rebuild Examine Asset Catalog** bakes `ExamineOverlayAssetCatalog` (a `Resources`-loaded icon/stylesheet catalog letting `ExamineOverlaySubSystem` self-bootstrap with no scene/prefab placement — a lighter one-off variant of the `UiAssetCatalogBase` pattern, since this surface has no `PanelSettings`/document of its own to carry). No `Human.prefab` recipe is needed.

**Condemned UI:** none remaining for this domain — the uGUI examine views were the last entry in [agent-first-composition.md](../2026-07_agent-first-composition.md)'s condemned table for Examine and are now deleted (see [TECH_DEBT.md](../TECH_DEBT.md) §1.8). Domain `IExaminable` / content resolution (`ExaminableBase`, `IExamineContentProvider`, resolvers) was never condemned and is unchanged.

## Start here

- `Assets/Scripts/SS3D/Systems/Examine/ExamineSubSystem.cs` — subsystem entry point; raises hover/detailed/character-window events
- `Assets/Scripts/SS3D/Systems/Examine/ExamineContentResolver.cs` — static table + dynamic section resolution
- `Assets/Scripts/SS3D/Systems/Examine/IExaminable.cs` — interface for examinable objects
- `Assets/Scripts/SS3D/Systems/Examine/ExaminableBase.cs` — base component; not an interaction target
- `Assets/Scripts/SS3D/Systems/Examine/ExamineData.cs` — ScriptableObject examine content asset (`Type` includes `CHARACTER`)
- `Assets/Scripts/SS3D/Systems/Examine/ExamineRangeUtility.cs` — closest-point range check reused by detailed-image gating
- `Assets/Scripts/SS3D/Localization/LocalizedTextService.cs` — shared localization accessor with caching
- `Assets/Scripts/SS3D/UI/Examine/ExamineOverlaySubSystem.cs` — owns every examine UI surface; hover/Shift-held/Shift+Click state machine
- `Assets/Scripts/SS3D/UI/Examine/GenericExamineHoverView.cs` — hover-name/detailed-text/detailed-image (`IUiSurface`)
- `Assets/Scripts/SS3D/UI/Examine/CharacterQuickLookView.cs` / `CharacterExamineWindowView.cs` — character hover preview / persistent window (`IUiSurface`)
- `Assets/Scripts/SS3D/Systems/Examine/CharacterExamineContentBuilder.cs` — reads any character's equipped items/visible identity from a `HumanInventory` (read-only, works on remote characters)
- `Assets/Scripts/SS3D/Systems/Examine/CharacterExamineSlotContainerMap.cs` — paperdoll slot ↔ `ContainerType` mapping
- All UITK examine code lives in the `SS3D.UI.Examine` assembly (references `SS3D.Systems`, `SS3D.UI.MachineInterface`, `SS3D.UI.Shell`) — kept out of `SS3D.Systems` deliberately, since `SS3D.UI.MachineInterface` (needed for the shared `InventorySlot`/`MachineWindow` components) already depends on `SS3D.Systems`, so the reverse reference would cycle.

## Extension points

- Add `SimpleExaminable`, `ImageExaminable`, or subclass `ExaminableBase` on world objects.
- Dynamic lines: implement `IExamineContentProvider` (see `IdentificationCardExaminable`, `StructuralIntegrityExaminable`).
- New character-like examinable: give its `ExamineData` asset `Type: CHARACTER` and make sure it has a `HumanInventory` sibling — no other wiring needed. `EngineeringBorg.asset` intentionally stays `SIMPLE_TEXT` (no compatible inventory yet).
- Editor: `SS3D/Localization/Examine/` menus for JSON export/import and template keys (`Editor/ExamineIdentificationKeySetup.cs`).
- Character examine follow-ups: a real "obscured by outer layer" flag on `Item`/`ClothingItemPresentation` (for covered-slot filtering), and a server-validated take/transfer request to back the hold-to-take stub (`CharacterExamineWindowView.CompleteHold`).

## Depends on / Used by

- **Depends on:** [selection](selection.md), [localization](localization.md), [inputs](inputs.md) (via selection pointer-over-UI clear), [inventory](inventory.md) (character examine reads `HumanInventory`/`ContainerType`/`Item.GetHudSprite`), [ui-shell](ui-shell.md) (`IUiSurface` views), [core-subsystems](core-subsystems.md) (`NetworkSystemsHub.prefab` — no longer hosts examine UI, only `ExamineSubSystem`)
- **Used by:** Most world objects with examine content

## Related docs

- Plan: [examine_localization_design_5ca361a6.plan.md](../../plans/examine_localization_design_5ca361a6.plan.md)
- Plan: [radial_menu_implementation_5a83bdf9.plan.md](../../plans/radial_menu_implementation_5a83bdf9.plan.md) § Examine tier (planned petal; not shipped as `IInteraction`)
- Design (read-only): [Documents/design/examine.md](../../design/examine.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- [TECH_DEBT.md](../TECH_DEBT.md) §1.1 (`Human.prefab` — why character examine adds nothing to it), §1.8 (condemned-UI backlog — this domain's entry resolved)
- [ui-shell](ui-shell.md)
- Tests: `ExamineContentResolverTests` (missing-key path must use synthetic keys — see [localization](localization.md) Pitfalls); `CharacterExamineContentBuilderTests`, `CharacterExamineSlotContainerMapTests`
