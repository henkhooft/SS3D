> Code paths: Assets/Scripts/SS3D/Systems/Examine/, Assets/Scripts/SS3D/UI/Examine/, Assets/Scripts/SS3D/Localization/
> Entry points: ExamineSubSystem, ExamineOverlaySubSystem, ExamineContentResolver, TakeFromCharacterInteraction
> Status: partial
> Verified: 0e6278889 — 2026-07-27

# Examine

## Overview

Hover tooltips and shift-hold detailed examine panels, range-gated off the [selection](selection.md) system's current `IExaminable`. Supports text and image panel variants. Localization uses a unified Examine string table and `LocalizedTextService`; dynamic content via `IExamineContentProvider` (e.g. identification cards).

Examine is **not** an `IInteraction` — `ExaminableBase` is read by `ExamineSubSystem` from the current selection (hover + Shift). There is no `ExamineInteraction` petal class on develop.

**Rendering is UI Toolkit** (`ExamineOverlaySubSystem`, `Assets/Scripts/SS3D/UI/Examine/`) — the previous uGUI `ExamineUI`/`ExamineDetailedView`/`ExamineImageDetailedView` trio has been deleted, not ported (Phase 0 purge). Surfaces: `GenericExamineHoverView` (hover name / Shift detailed text or image); `CharacterQuickLookView` / `CharacterExamineWindowView` (character paperdoll).

**Fork deviations from** [examine.md](../../design/examine.md): design is hold-to-peek with no hover/click-lock (§1/§4); shipped code always shows the name tooltip on plain hover, with Shift gating richer detail. Accepted.

**Character examine** (design §7): no new `Human.prefab` component — `Human.asset` is `Type: CHARACTER`; overlay reads sibling `HumanInventory`. Do **not** reintroduce `CharacterExaminable` ([TECH_DEBT.md](../TECH_DEBT.md) §1.1).

- **Hover** → name tooltip only (`GenericExamineHoverView`, identity from ID when visible).
- **Shift+Click** → persistent `CharacterExamineWindowView` paperdoll (× to close). Routed via `ExamineSubSystem.OnCharacterWindowRequested`. No hover paperdoll — a pickable quick-look under the cursor blocked Shift+Click via `IsPointerOverInterface` and cleared selection.
- **Hold-to-take** — ~1.5s `TakeFromCharacterInteraction` (`DelayedInteraction`: cancel on move/range/Backspace/pointer-up). Server `Hand.Pickup` into the active hand. UITK slot spinner (`InventorySlot.SetTakeProgress`); no world LoadingBar. Gated by `CharacterLootUtility.IsLootable` (dead or unconscious; restrained deferred). Living conscious characters are examine-only.
- **Fork vs** [inventory-storage.md](../../design/inventory-storage.md) §9: design opens foreign gear as StoragePanels + drag; this pass keeps paperdoll delayed take. Accepted for now.
- **Covered/obscured-slot filtering** not implemented (needs clothing “hidden by outer layer” data).
- Slot↔`ContainerType`: `CharacterExamineSlotContainerMap` (Back→Bag, Shirt→Jumpsuit, Suit→ExoSuit, paired ears/feet/gloves).

**Editor:** **SS3D → Examine → Rebuild Examine Asset Catalog**. Condemned uGUI examine UI is gone ([TECH_DEBT.md](../TECH_DEBT.md) §1.8).

## Start here

- `ExamineSubSystem.cs` — hover/detailed/character-window events
- `ExamineContentResolver.cs` / `ExamineData.cs` / `IExaminable.cs` / `ExaminableBase.cs`
- `ExamineRangeUtility.cs` — detailed-image range
- `LocalizedTextService.cs` — shared localization accessor
- `UI/Examine/ExamineOverlaySubSystem.cs` — all examine UI surfaces + take wiring
- `UI/Examine/GenericExamineHoverView.cs` / `CharacterQuickLookView.cs` / `CharacterExamineWindowView.cs`
- `CharacterExamineContentBuilder.cs` — slot icons + `TryGetItemInSlot` / visible identity
- `CharacterExamineSlotContainerMap.cs` — paperdoll ↔ `ContainerType`
- `Inventory/Containers/CharacterLootUtility.cs` — dead/unconscious loot gate
- `Inventory/Interactions/TakeFromCharacterInteraction.cs` — delayed take windup
- `Interactions/InteractionController.cs` — `RequestTakeFromCharacter` / cancel (not Discover)
- UITK examine is `SS3D.UI.Examine` (avoids Systems↔MachineInterface cycle)

## Extension points

- `SimpleExaminable` / `ImageExaminable` / `IExamineContentProvider` on world objects.
- New CHARACTER examinable: `ExamineData.Type: CHARACTER` + `HumanInventory` sibling.
- Follow-ups: obscured-slot filtering; restrained loot gate; StoragePanel foreign-loot path.

## Pitfalls

- **Take without Discover:** paperdoll slots are not `IInteractionTarget`s — start via `InteractionController.RequestTakeFromCharacter`, never `CmdRunInteraction` name resolve.
- **Foreign `CmdTransferItem` rejects** containers not in `ContainerViewer` display list — take uses `Hand.Pickup` after lootability checks instead.
- **Catalog missing in builds:** run **Rebuild Examine Asset Catalog** and commit `Resources` asset.
- **`InventorySlot` white HUD:** never put `overflow: hidden` on `.inventory-slot__well` (it already has `border-radius`) — Main HUD shares that USS and turns into a flat white block. Take-progress spinner stays inset without clipping.
- **DDOL overlay vs Online hub:** `ExamineOverlaySubSystem` is AfterSceneLoad DDOL; `ExamineSubSystem` only exists on `NetworkSystemsHub` after Online. One-shot subscribe in `OnEnabled` misses the hub — bind must retry in `Update` (and rebind after hub teardown).
- **Examine ↔ Selection enable order:** `ExamineSubSystem` can `OnEnable` before `SelectionSubSystem` is registered. One-shot subscribe misses Selection — retry in `Update` (same pattern as overlay→examine). Without it, hover never fires `OnExaminableChanged` even though Selection picks items.
- **Hover label off-screen / invisible:** UiShell `PanelSettings` is `ConstantPhysicalSize`. Cursor anchors must use `InputInterface.ScreenToPanel` then `style.left`/`style.top` (same as `ZoneTargetReticle`). Raw mouse + `style.bottom` places the label wrong / off-panel — silent, no error.
- **Character quick-look “far below” cursor:** Switching bottom-edge mouse anchors to `style.top` without a `-100%` Y translate hangs the tall paperdoll under the pointer. Keep `-100%` translate (or subtract resolved height) so the panel sits above the cursor like the old `style.bottom` layout.
- **Hover paperdoll blocks Shift+Click:** A pickable paperdoll under the cursor makes `IsPointerOverInterface` true → selection clears and `HandleRunPrimary` never opens the window. Character paperdoll is Shift+Click-only; hover is name tooltip.
- **Shift+Click vs Toggle Run:** sprint is Caps Lock; Shift is examine-only
  ([2026-07_default-input-scheme.md](../2026-07_default-input-scheme.md)). Prefer
  `Keyboard.current.*ShiftKey.isPressed` for the examine modifier when the code-defined
  `DetailedExamine` action can miss while another map also reads Shift.
- **No `box-shadow` in USS:** UI Toolkit rejects it (Unknown style property). Use a 1px border for elevation; never combine `border-radius` + `overflow: hidden` on the same examine element either.

## Depends on / Used by

- **Depends on:** [selection](selection.md), [localization](localization.md), [inputs](inputs.md), [inventory](inventory.md), [health](health.md) (lootability), [interactions-framework](interactions-framework.md) (`DelayedInteraction`), [ui-shell](ui-shell.md), [core-subsystems](core-subsystems.md)
- **Used by:** Most world objects with examine content

## Related docs

- Plan: [examine_localization_design_5ca361a6.plan.md](../../plans/examine_localization_design_5ca361a6.plan.md)
- Design (read-only): [Documents/design/examine.md](../../design/examine.md), [inventory-storage.md](../../design/inventory-storage.md) §9
- Defaults: [2026-07_default-input-scheme.md](../2026-07_default-input-scheme.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md), [TECH_DEBT.md](../TECH_DEBT.md) §1.1 / §1.8
- Tests: `ExamineContentResolverTests`, `CharacterExamineContentBuilderTests`, `CharacterExamineSlotContainerMapTests`, `CharacterLootUtilityTests`
