> Code paths: Assets/Scripts/SS3D/Systems/Chat/, Assets/Scripts/SS3D/Systems/Comms/, Assets/Scripts/SS3D/Systems/Audio/, Assets/Scripts/SS3D/Systems/Screens/
> Entry points: ChatSubSystem, CommsSubSystem, AudioSubSystem, PlayerCameraSubSystem
> Status: stub

# Chat / audio / screens

## Overview

In-game chat, audio playback, and camera/screen controllers. (Navigation map not yet fully reviewed.)

**Condemned UI:** always-on chat window — do not extend or port to UITK; replace per [comms.md](../../design/comms.md) ([agent-first composition](../2026-07_agent-first-composition.md)). The in-game `ToggleChatsButton` on `PlayerCanvas` is disabled (obsolete chrome).

## Start here

- `Assets/Scripts/SS3D/Systems/Chat/ChatSubSystem.cs` — legacy always-on scrolling chat backend (UGUI window). Left untouched; comms.md §2 cuts it as the *primary* interface, its content becomes the future PDA log slice.
- `Assets/Scripts/SS3D/Systems/Comms/CommsSubSystem.cs` — new local-speech-bubble system (comms.md §3–§4 vertical slice 1: local speech + crowd cap). See `Assets/Scripts/SS3D/Systems/Comms/` for the full slice (`LocalSpeechEmitter`, `LocalSpeechListener`, `LocalSpeechBubbleController`, `CrowdCapRanker`, `LocalSpeechConfig`). Whisper, shout, radio/channels, non-diegetic feed, announcements, and the PDA log are not built yet — see the seams noted in each class's doc comment.
- `Assets/Scripts/SS3D/Systems/Audio/AudioSubSystem.cs` — audio subsystem
- `Assets/Scripts/SS3D/Systems/Screens/PlayerCameraSubSystem.cs` — player camera
- `Assets/Scripts/SS3D/Systems/Screens/CameraSubSystem.cs` — camera subsystem

## Manual Editor setup required for the local speech slice

The code and content assets are committed, but three placements need a human with the Unity Editor open (no safe way to hand-edit these serialized files blind):

1. Add a `CommsSubSystem` component to a persistent GameObject in `Assets/Content/Scenes/Game.unity` (mirrors the `ChatSystem` GameObject that already hosts `ChatSubSystem`, fileID `478615866`).
2. Add a `LocalSpeechEmitter` component to `Assets/Content/WorldObjects/Entities/Humanoids/Human/Human.prefab` (the player entity prefab spawned by `EntitySubSystem`).
3. Add a GameObject with a `UIDocument` + `LocalSpeechBubbleController` to `Game.unity` (or a persistent UI root), with `CommsOverlayPanelSettings.asset` assigned to the `UIDocument` and `LocalSpeechBubble.uss` / `LocalSpeechConfig.asset` assigned on the controller — `LocalSpeechBubbleController.EnsureEditorAssets()` auto-fills these three from their known paths under `Assets/Content/Systems/UI/Comms/LocalSpeechBubbles/` the first time the component awakens in the Editor, so this step just needs the GameObject + component added.

`LocalSpeechDebugTrigger` (F3 to speak a test line as the local player) and `TextGarbler`/tier logic need no wiring — they self-bootstrap or are plain C#.

## Extension points

(stub)

## Depends on / Used by

- **Depends on:** [player-control](player-control.md)

## Related docs

- Design (read-only): [Documents/design/comms.md](../../design/comms.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
