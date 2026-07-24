> Code paths: Assets/Scripts/SS3D/Systems/Examine/, Assets/Scripts/SS3D/UI/Examine/, Assets/Scripts/SS3D/Localization/
> Entry points: ExamineSubSystem, ExamineUI, ExamineContentResolver, CharacterExamineSubSystem
> Status: partial
> Verified: 2026-07-24

# Examine

## Overview

Hover tooltips and shift-hold detailed examine panels, range-gated off the [selection](selection.md) system's current `IExaminable`. Supports text and image panel variants. Localization uses a unified Examine string table and `LocalizedTextService`; dynamic content via `IExamineContentProvider` (e.g. identification cards).

Examine is **not** an `IInteraction` — `ExaminableBase` is read by `ExamineSubSystem` from the current selection (hover + Shift). There is no `ExamineInteraction` petal class on develop.

**Fork deviations from** [examine.md](../../design/examine.md): design specifies a single momentary hold-to-peek key with no hover/click-lock state (§1/§4); shipped code always shows the name tooltip on plain hover (`ExamineUI.UpdateHoverText`), with Shift only gating the richer detailed content. Accepted as intentional UX direction, not scheduled for rework.

**Character examine (new, UITK, `SS3D.UI.Examine`)** implements design §7's "examine another character" target type — this closes the gap the previous version of this doc flagged (no `IExaminable`/content provider existed anywhere under `Systems/Entities/` or on `Human*.prefab`). It is a **second, deliberate departure** from §4's hold-to-peek/no-click-lock discipline, scoped to characters only:
- **Hover** → `CharacterQuickLookView` shows a compact paperdoll preview unconditionally (no Shift needed) — different from every other examinable, which shows only a name on hover.
- **Shift+Click** on a character → `CharacterExamineWindowView`, a *persistent* window (only its × closes it, no auto-close on mouse-leave/re-hover) — the click-lock state §4 explicitly rules out for the general case. Wired in `InteractionController.HandleRunPrimary` (checks `InputSubSystem.DetailedExamine.IsPressed()` + `SelectionSubSystem.GetCurrentSelectable<CharacterExaminable>()`), routed through a new `ExamineSubSystem.OnCharacterWindowRequested` event rather than a direct reference, so the interactions layer never depends on the UI-layer window.
- **Hold-to-take** (~650ms hold on an equipped slot) is a **UI-only stub**: shows the hold/pulse affordance and clears the slot's displayed icon locally, but does **not** perform any networked item transfer — `CharacterExamineWindowView.CompleteHold` is explicitly marked `FOLLOW-UP` for whoever designs the actual loot-another-character permission/range model.
- **Covered/obscured-slot filtering is not implemented** — no existing `Item`/`ClothingItemPresentation` data tracks "hidden by an outer worn layer," so every equipped item currently shows regardless of what's worn over it. Revisit once that data exists.
- Slot↔`ContainerType` mapping (`CharacterExamineSlotContainerMap`) reuses Main HUD's precedent for overlapping slots (Back→Bag, Shirt→Jumpsuit, paired Ears/Feet/Gloves) and gives the previously-unused `ContainerType.ExoSuit` its first real UI surface (mapped from the paperdoll's "Suit" row).
- Requires two one-time Editor steps before it works in-game (no code fix — asset baking only): **SS3D → Entities → Run All Human Prefab Recipes** (adds `Selectable` + a root range-check `CapsuleCollider` + `CharacterExaminable` to `Human.prefab`, via `CharacterExaminePrefabSetup`) and **SS3D → Examine → Rebuild Character Examine Asset Catalog** (bakes `CharacterExamineAssetCatalog`, a `Resources`-loaded icon/stylesheet catalog letting `CharacterExamineSubSystem` self-bootstrap with no scene/prefab placement — a lighter one-off variant of the `UiAssetCatalogBase` pattern, since this surface has no `PanelSettings`/document of its own to carry).

**Condemned UI:** examine hover/detailed uGUI views — do not extend; rebuild on UITK when HUD/examine redesign lands ([agent-first composition](../2026-07_agent-first-composition.md)). Domain `IExaminable` / content resolution is **not** condemned. Character examine above is the first UITK rebuild under this policy for this domain; the Shift-hold/name-only hover path (`ExamineUI`) remains condemned uGUI, not yet replaced.

## Start here

- `Assets/Scripts/SS3D/Systems/Examine/ExamineSubSystem.cs` — subsystem entry point; raises hover/detailed events
- `Assets/Scripts/SS3D/Systems/Examine/ExamineUI.cs` — hover and detailed panel UI (Shift-hold)
- `Assets/Scripts/SS3D/Systems/Examine/ExamineContentResolver.cs` — static table + dynamic section resolution
- `Assets/Scripts/SS3D/Systems/Examine/IExaminable.cs` — interface for examinable objects
- `Assets/Scripts/SS3D/Systems/Examine/ExaminableBase.cs` — base component; not an interaction target
- `Assets/Scripts/SS3D/Systems/Examine/ExamineData.cs` — ScriptableObject examine content asset
- `Assets/Scripts/SS3D/Localization/LocalizedTextService.cs` — shared localization accessor with caching
- `Assets/Scripts/SS3D/Systems/Examine/CharacterExaminable.cs` — character-root `IExaminable`; exposes `HumanInventory`
- `Assets/Scripts/SS3D/Systems/Examine/CharacterExamineContentBuilder.cs` — reads any character's equipped items/visible identity (read-only, works on remote characters)
- `Assets/Scripts/SS3D/Systems/Examine/CharacterExamineSlotContainerMap.cs` — paperdoll slot ↔ `ContainerType` mapping
- `Assets/Scripts/SS3D/UI/Examine/CharacterExamineSubSystem.cs` — hover/Shift+Click state machine; owns both character-examine UITK views
- `Assets/Scripts/SS3D/UI/Examine/CharacterQuickLookView.cs` / `CharacterExamineWindowView.cs` — hover preview / persistent window (`IUiSurface`, attach into `UiShellSubSystem`'s Overlay layer)
- `Assets/Scripts/SS3D/Systems/Entities/Editor/CharacterExaminePrefabSetup.cs` — one-time `Human.prefab` wiring (registered in `HumanPrefabRecipes`)

## Extension points

- Add `SimpleExaminable`, `ImageExaminable`, or subclass `ExaminableBase` on world objects.
- Dynamic lines: implement `IExamineContentProvider` (see `IdentificationCardExaminable`, `StructuralIntegrityExaminable`).
- Editor: `SS3D/Localization/Examine/` menus for JSON export/import and template keys (`Editor/ExamineIdentificationKeySetup.cs`).
- Character examine follow-ups: a real "obscured by outer layer" flag on `Item`/`ClothingItemPresentation` (for covered-slot filtering), and a server-validated take/transfer request to back the hold-to-take stub (`CharacterExamineWindowView.CompleteHold`).

## Depends on / Used by

- **Depends on:** [selection](selection.md), [localization](localization.md), [inputs](inputs.md) (via selection pointer-over-UI clear), [inventory](inventory.md) (character examine reads `HumanInventory`/`ContainerType`/`Item.GetHudSprite`), [ui-shell](ui-shell.md) (character examine's `IUiSurface` views)
- **Used by:** Most world objects with examine content

## Related docs

- Plan: [examine_localization_design_5ca361a6.plan.md](../../plans/examine_localization_design_5ca361a6.plan.md)
- Plan: [radial_menu_implementation_5a83bdf9.plan.md](../../plans/radial_menu_implementation_5a83bdf9.plan.md) § Examine tier (planned petal; not shipped as `IInteraction`)
- Design (read-only): [Documents/design/examine.md](../../design/examine.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- [ui-shell](ui-shell.md)
- Tests: `ExamineContentResolverTests` (missing-key path must use synthetic keys — see [localization](localization.md) Pitfalls)
