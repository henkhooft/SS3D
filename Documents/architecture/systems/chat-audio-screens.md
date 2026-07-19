> Code paths: Assets/Scripts/SS3D/Systems/Chat/, Assets/Scripts/SS3D/Systems/Comms/, Assets/Scripts/SS3D/Systems/Audio/, Assets/Scripts/SS3D/Systems/Screens/
> Entry points: ChatSubSystem, CommsSubSystem, AudioSubSystem, PlayerCameraSubSystem
> Status: stub
> Verified: 053f21733 — 2026-07-19

# Chat / audio / screens

## Overview

In-game chat, audio playback, and camera/screen controllers. (Navigation map not yet fully reviewed.)

**Condemned UI:** always-on chat window — do not extend or port to UITK; replace per [comms.md](../../design/comms.md) ([agent-first composition](../2026-07_agent-first-composition.md)). The in-game `ToggleChatsButton` on `PlayerCanvas` is disabled (obsolete chrome).

Local speech (comms slice 1) follows the Claude Design **weighted chips** mock (option 1a): soft `rgba(10,10,10,~0.28)` plate, 10px radius, Gurajada uppercase name on the newest line only, UI-sans quoted dialogue, older stack lines shed name/quotes and fade, with distinct whisper/shout/emote/radio/announce treatments.

## Start here

- `Assets/Scripts/SS3D/Systems/Chat/ChatSubSystem.cs` — legacy always-on scrolling chat backend (UGUI window). Left untouched; comms.md §2 cuts it as the *primary* interface, its content becomes the future PDA log slice.
- `Assets/Scripts/SS3D/Systems/Comms/CommsSubSystem.cs` — new local-speech-bubble system (comms.md §3–§4 vertical slice 1: local speech + crowd cap). See `Assets/Scripts/SS3D/Systems/Comms/` for the full slice (`LocalSpeechEmitter`, `LocalSpeechListener`, `LocalSpeechBubbleController`, `CrowdCapRanker`, `LocalSpeechConfig`). Whisper/shout/emote compose input, radio/channels, non-diegetic feed, announcements, and the PDA log are not built yet. F3 only previews **local** modes (speak/whisper/shout/emote).
- `Assets/Scripts/SS3D/Systems/Audio/AudioSubSystem.cs` — audio subsystem
- `Assets/Scripts/SS3D/Systems/Screens/PlayerCameraSubSystem.cs` — player camera
- `Assets/Scripts/SS3D/Systems/Screens/CameraSubSystem.cs` — camera subsystem

## Manual Editor setup required for the local speech slice

Scene/prefab placements for the local speech slice are already in `Game.unity` (`CommsSystem`, `LocalSpeechBubblesSystem`) and `Human.prefab` (`LocalSpeechEmitter`). Re-check those if a fresh scene/prefab loses the wiring. `LocalSpeechBubbleController.EnsureEditorAssets()` still auto-fills USS / config / PanelSettings from `Assets/Content/Systems/UI/Comms/LocalSpeechBubbles/` when missing in the Editor.

`LocalSpeechDebugTrigger` (F3 cycles local-chat test lines: speak / whisper / shout / emote) and `TextGarbler`/tier logic need no wiring — they self-bootstrap or are plain C#.

## Pitfalls

- **Speech bubbles invisible with healthy speech logs:** if `ShowBubble` reports `panel=null` / `resolvedSize=(NaNxNaN)`, the controller attached to a `UIDocument.rootVisualElement` that is not (or no longer) on a live panel. `EnsureOverlay` must require `root.panel != null`, compare against the current root identity, and tear down on disable — UIDocument rebuilds its tree across disable/enable and a cached view will keep driving orphans forever.
- **Do not extend the condemned always-on chat window** — replace per [comms.md](../../design/comms.md).
- **Local speech presentation vs design:** in-game local speech follows the Claude Design weighted-chips mock (soft translucent plate, Gurajada name + UI-sans line, stack/drift, mode CSS). This diverges from [comms.md](../../design/comms.md) §3's flat HUD chip + "attribution is free / no name prefix," and from §6–§8 routing radio/announcements exclusively to the non-diegetic feed (those modes are styled here for preview only until that feed exists). Recorded here on purpose — do not edit the design doc to match.
- **UITK masking:** do not combine `border-radius` with `overflow: hidden` on the same subtitle element (renders as a flat white block — same pitfall as machine-interface).

## Extension points

(stub)

## Depends on / Used by

- **Depends on:** [player-control](player-control.md)

## Related docs

- Design (read-only): [Documents/design/comms.md](../../design/comms.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
