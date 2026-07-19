> Code paths: Assets/Scripts/SS3D/Systems/Chat/, Assets/Scripts/SS3D/Systems/Comms/, Assets/Scripts/SS3D/Systems/Audio/, Assets/Scripts/SS3D/Systems/Screens/
> Entry points: ChatSubSystem, CommsSubSystem, AudioSubSystem, PlayerCameraSubSystem
> Status: partial
> Verified: 0b7a60e7d — 2026-07-19

# Chat / audio / screens

## Overview

In-game chat backend, local-speech UI, audio playback, and camera/screen controllers. (Navigation map not yet fully reviewed.)

**Always-on chat UI Phase 0 purged** per [comms.md](../../design/comms.md) + [agent-first composition](../2026-07_agent-first-composition.md): in-game/lobby chat windows, `ToggleChats`, tabs, and `InGameChatController` are gone. Do **not** resurrect UGUI chat chrome. `ChatSubSystem` remains headless (station alerts + future PDA/log / non-diegetic feed).

Local speech (comms slice 1) follows the Claude Design **weighted chips** mock (option 1a): soft translucent plate, Gurajada uppercase name on the newest line only, UI-sans dialogue, older stack lines shed name/quotes and fade. Compose (mock 2a / comms.md §5): **T** opens a draft chip at the same head anchor as a finished line — dashed outline on the outer plate edge + real TextField caret, high-contrast body type — Enter sends speak, Shift+Enter whisper, Ctrl+Enter shout, Escape cancels. Typing holds `TextEntry` + `InputInterface` text capture so gameplay input is fully masked.

## Start here

- `Assets/Scripts/SS3D/Systems/Chat/ChatSubSystem.cs` — headless message hub (FishNet broadcast, server log file, `SendPlayerMessage` / `SendServerMessage*`). Channel SOs under `Assets/Content/Data/UI/Chat/Channels/`. Round/Entity still post station alerts here; with no UI subscribers those messages are fire-and-forget until the non-diegetic feed / PDA log lands.
- `Assets/Scripts/SS3D/Systems/Comms/CommsSubSystem.cs` — local-speech system hub. Slice under `Assets/Scripts/SS3D/Systems/Comms/`: `LocalSpeechEmitter`, `LocalSpeechListener`, `LocalSpeechBubbleController` (overlay + compose), `LocalSpeechBubbleView`, `CrowdCapRanker`, `LocalSpeechConfig`. Radio/channels, non-diegetic feed, announcements, PDA log not built yet. F3 (`LocalSpeechDebugTrigger`) still cycles local test lines.
- `Assets/Scripts/SS3D/Systems/Audio/AudioSubSystem.cs` — audio subsystem
- `Assets/Scripts/SS3D/Systems/Screens/PlayerCameraSubSystem.cs` — player camera
- `Assets/Scripts/SS3D/Systems/Screens/CameraSubSystem.cs` — camera subsystem

## Manual Editor setup required for the local speech slice

Scene/prefab placements for the local speech slice are already in `Game.unity` (`CommsSystem`, `LocalSpeechBubblesSystem`) and `Human.prefab` (`LocalSpeechEmitter`). Re-check those if a fresh scene/prefab loses the wiring. `LocalSpeechBubbleController.EnsureEditorAssets()` still auto-fills USS / config / PanelSettings from `Assets/Content/Systems/UI/Comms/LocalSpeechBubbles/` when missing in the Editor.

## Pitfalls

- **Speech bubbles invisible with healthy speech logs:** if `ShowBubble` reports `panel=null` / `resolvedSize=(NaNxNaN)`, the controller attached to a `UIDocument.rootVisualElement` that is not (or no longer) on a live panel. `EnsureOverlay` must require `root.panel != null`, compare against the current root identity, and tear down on disable — UIDocument rebuilds its tree across disable/enable and a cached view will keep driving orphans forever.
- **Do not resurrect UGUI always-on chat** — UI purged; headless `ChatSubSystem` only until the non-diegetic feed / PDA log per [comms.md](../../design/comms.md).
- **Station alerts are silent for now:** `RoundSubSystem` / `EntitySubSystem` still call `ChatSubSystem.SendServerMessage`; nothing displays them until a feed UI subscribes to `OnMessageReceived`.
- **Local speech presentation vs design:** in-game local speech follows the Claude Design weighted-chips mock (soft translucent plate, Gurajada name + UI-sans line, stack/drift, mode CSS). This diverges from [comms.md](../../design/comms.md) §3's flat HUD chip + "attribution is free / no name prefix," and from §6–§8 routing radio/announcements exclusively to the non-diegetic feed (those modes are styled here for preview only until that feed exists). Recorded here on purpose — do not edit the design doc to match.
- **UITK masking:** do not combine `border-radius` with `overflow: hidden` on the same subtitle element (renders as a flat white block — same pitfall as machine-interface).
- **UITK has no `border-style: dashed`:** draft chip outline is painted via `generateVisualContent` / Painter2D in `LocalSpeechBubbleView`, not USS.
- **Compose input:** open with arbitrated `InputSubSystem.OpenLocalSpeechCompose` (T). While drafting hold `InputContext.TextEntry` via `InputTextEntryScope` (all Input System actions off; Enter/Escape via UITK `KeyDownEvent` on TrickleDown so multiline wrap does not eat the first Enter). `InputTextEntryScope` also pushes `InputInterface` text capture so `IsPointerOverInterface()` / `IsCapturingText` stay true. Keyboard-polled debug toggles (H health, F3 speech, Atmos P fallback) must check `IsCapturingText`.
- **Compose focus lock:** while drafting, `SetRetainDraftFocus(true)` re-focuses the TextField on `FocusOutEvent` so a world click cannot leave TextEntry held with no focused field (keys go nowhere). Escape/Enter still end compose and clear the lock.
- **Draft width measure:** do not call `TextField.MeasureTextSize` after setting `style.width` — it returns the laid-out width and hug-sizing stalls until a mode/wrap invalidation. Measure via an off-screen Label proxy instead.
- **Draft TextField type:** do not rely on nested USS `font-size` / `color` on the TextField — `.font-body` (11px secondary) and UITK's input tree ignore those rules the same way they ignore `-unity-text-align`. Force size/color via `ApplyDraftTypeStyles` in `LocalSpeechBubbleView`.
- **Draft head centering:** do not use USS `translate: -50%` on the draft chip — UITK keeps a stale translate transform while width changes every keystroke. Set `left = headX - width/2` in `ApplyDraftScreenPosition`.

## Extension points

- Mode-pip fading hints (comms.md §5) and whisper presence-while-typing for *other* viewers are still open.
- Non-diegetic feed / PDA log should subscribe to `ChatSubSystem.OnMessageReceived` (or replace that hub) rather than rebuilding UGUI chat.
- Radio compose (channel radial → pre-tagged draft) waits on the non-diegetic feed.

## Depends on / Used by

- **Depends on:** [player-control](player-control.md), [inputs](inputs.md)
- **Used by:** [rounds-lobby](rounds-lobby.md) (station alerts via `SendServerMessage`)

## Related docs

- Design (read-only): [Documents/design/comms.md](../../design/comms.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- [2026-07_input-arbitration](../2026-07_input-arbitration.md)
