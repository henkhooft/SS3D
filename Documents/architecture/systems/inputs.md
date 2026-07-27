> Code paths: Assets/Scripts/SS3D/Systems/Inputs/, Assets/Content/Systems/Input/
> Entry points: InputSubSystem, InputArbiter, InputInterface
> Status: partial
> Verified: 0e6278889 — 2026-07-27

# Inputs

## Overview

Central input layer wrapping the Unity Input System. Two responsibilities:

1. **Arbitration** — which actions are live at any moment. State is *derived from a set of owned,
   self-releasing requests*, never from a shared counter. Callers push an `InputContext` or a
   suppression and receive an `IInputHandle`; disposing the handle removes exactly that request.
   `InputArbiter` recomputes each action's `enabled` flag from the live request set as the single
   writer, so dead keys, leaked input, and enabled/refcount desync are structurally impossible.
2. **Pointer authority** — `InputInterface.IsPointerOverInterface()` is the one place that answers
   "is the pointer over UI", spanning uGUI (`GraphicRaycaster`) and UI Toolkit (`panel.Pick`).
   It is also true while `InputTextEntryScope` holds a text-capture (compose / focused fields) so
   world clicks and selection clear for the whole typing session, not only when the cursor is over
   the field. Callers include interaction click gates and selection hover clearing (examine/outlines).

**Default key layout** (Caps sprint, Shift examine, F intent, Backspace cancel, E = Use only):
[2026-07_default-input-scheme.md](../2026-07_default-input-scheme.md). See that doc before adding
bindings. Arbitration model: [2026-07_input-arbitration.md](../2026-07_input-arbitration.md).

## Start here

- `Assets/Scripts/SS3D/Systems/Inputs/InputSubSystem.cs` — owns `Controls`, builds the context table,
  exposes `PushContext` / `SuppressMap` / `SuppressAction` / `SuppressBinding` and the code-defined
  `UiCancel` / `DetailedExamine` / `ToggleIntent` / `OpenLocalSpeechCompose` / `ToggleAlertStackDebug`
  actions.
- `Assets/Content/Systems/Input/Controls.inputactions` — asset bindings (keep in sync with generated
  `Controls.cs` when regenerating).
- `Assets/Scripts/SS3D/Systems/Inputs/InputArbiter.cs` — pure resolution engine (unit tested).
- `Assets/Scripts/SS3D/Systems/Inputs/InputContext.cs` — the context enum (value = priority).
- `Assets/Scripts/SS3D/Systems/Inputs/InputInterface.cs` — unified pointer query + document registry.
- `Assets/Scripts/SS3D/Systems/Inputs/InputTextEntryScope.cs` — shared focus helper for text fields.

## Extension points

- **Need input while some UI/state is active?** Add a value to `InputContext` (higher value = higher
  priority) and a matching `InputContextDefinition` entry in `InputSubSystem.BuildContexts()`. Push it
  from the owner and dispose on teardown.
- **Need to temporarily block a key/map?** Use `SuppressBinding` / `SuppressMap` / `SuppressAction`
  and dispose the handle when done. Prefer disposing in `OnDisabled`/`OnDestroyed` so a missed
  pointer-exit or early disable can never strand the suppression.
- **New runtime UI Toolkit panel that should block world clicks?** Call
  `InputInterface.RegisterDocument` in setup and `UnregisterDocument` in teardown — unless the document is a
  shared one owned by `UiShellSubSystem` ([ui-shell](ui-shell.md)), in which case register (idempotent) but don't
  unregister on your own surface's teardown, since other surfaces sharing that document still need it registered.
- **Need a one-off debug/UI chord without regenerating `Controls.cs`?** Add a code-defined action on
  `InputSubSystem`'s `System` map (see `UiCancel`, `ToggleIntent`, `OpenLocalSpeechCompose`,
  `ToggleAlertStackDebug`), include it in the contexts that should enable it, and subscribe to `performed`.
  Prefer a console command first for debug; hotkey panels are debt — [TECH_DEBT.md](../TECH_DEBT.md) §1.13.
- **Tab ownership:** compose channel cycle owns Tab while drafting (Keyboard poll in
  `LocalSpeechBubbleController`). Legacy objectives `Other/Fade` Tab binding is erased at startup.
  Unused `Other/Attack` + `Show Owner` (both were F) are also erased so `ToggleIntent` owns F.

## Conventions

- Never call `InputAction.Enable/Disable` directly; go through a context or suppression handle.
- A handle must be owned by exactly one object and disposed once; disposing is idempotent and
  order-independent, so out-of-order release across objects is safe.
- Do not bind debug views to gameplay letters (E/C/F/Q/X). Check the default scheme doc first.

## Pitfalls

- **`EventSystem.IsPointerOverGameObject()` + UI Toolkit:** the UITK `PanelRaycaster` reports the whole
  `UIDocument` as a hit, including `PickingMode.Ignore` layout roots. Fullscreen shells (map editor)
  then look like invisible UI and cancel world placement on mouse-up. `InputInterface` uses
  `panel.Pick` for UITK (honours Ignore) and only `GraphicRaycaster` hits for uGUI — never the
  parameterless `IsPointerOverGameObject()` shortcut.
- **Registered `UIDocument` root steals picks:** the document root defaults to `PickingMode.Position` and
  often fills the screen (Main HUD). Even with content `display:none` / Ignore children, `panel.Pick`
  hits the root. `RegisterDocument` forces the root to `Ignore`; content must opt in with Position.
- **UITK pick Y is top-left, mouse is bottom-left:** before `RuntimePanelUtils.ScreenToPanel` / `panel.Pick`,
  flip with `y = Screen.height - y` (or use `InputInterface.ScreenToPanel`). Skipping the flip maps the
  top of the screen onto bottom chrome (map editor Object Library) — an invisible full-width block in the
  upper half, while the lower UI appears click-through.
- **Regenerating `Controls.cs`:** re-apply Caps / Backspace / F6 (and startup erasures) if the generator
  restores old paths — asset + generated wrapper must match [2026-07_default-input-scheme.md](../2026-07_default-input-scheme.md).

## Tests

- EditMode: `Assets/Scripts/Tests/EditMode/InputArbiterTests.cs`,
  `Assets/Scripts/Tests/EditMode/InputInterfaceTests.cs`.

## Depends on / Used by

- **Used by:** [player-control](player-control.md), [interactions-runtime](interactions-runtime.md),
  [machine-interface](machine-interface.md), [chat-audio-screens](chat-audio-screens.md),
  [tile](tile.md), [examine](examine.md), [ingame-console](ingame-console.md), [inventory](inventory.md),
  [entities](entities.md) (intent toggle)

## Related docs

- [2026-07_default-input-scheme.md](../2026-07_default-input-scheme.md)
- [2026-07_input-arbitration.md](../2026-07_input-arbitration.md)
- [INDEX.md](../INDEX.md)
