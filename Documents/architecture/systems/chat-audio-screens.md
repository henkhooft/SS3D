> Code paths: Assets/Scripts/SS3D/Systems/Chat/, Assets/Scripts/SS3D/Systems/Audio/, Assets/Scripts/SS3D/Systems/Screens/
> Entry points: ChatSubSystem, AudioSubSystem, PlayerCameraSubSystem, CameraSubSystem, CameraFollow
> Status: stub
> Verified: 69caa4010 — 2026-07-18

# Chat / audio / screens

## Overview

In-game chat, audio playback, and camera/screen controllers. Camera pose is still multi-writer (`CameraFollow`, map-editor session, FOV tweens, ad-hoc `Camera.main`) — do not add new modal camera drivers that poke `CameraFollow` or `Camera.main` directly; planned fix is [camera ownership](../2026-07_camera-ownership.md) (dedicated manager / contexts, same ownership rule as input arbitration).

**Condemned UI:** always-on chat window — do not extend or port to UITK; replace per [comms.md](../../design/comms.md) ([agent-first composition](../2026-07_agent-first-composition.md)). The in-game `ToggleChatsButton` on `PlayerCanvas` is disabled (obsolete chrome).

## Start here

- `Assets/Scripts/SS3D/Systems/Chat/ChatSubSystem.cs` — chat subsystem
- `Assets/Scripts/SS3D/Systems/Audio/AudioSubSystem.cs` — audio subsystem
- `Assets/Scripts/SS3D/Systems/Screens/PlayerCameraSubSystem.cs` — binds follow target on local player spawn
- `Assets/Scripts/SS3D/Systems/Screens/CameraSubSystem.cs` — holds `PlayerCamera` Actor reference
- `Assets/Scripts/SS3D/Systems/Screens/CameraFollow.cs` — gameplay orbit-follow; Coimbra `UpdateEvent` must guard `isActiveAndEnabled`

## Extension points

(stub — prefer waiting on [camera ownership](../2026-07_camera-ownership.md) before new camera modes)

## Pitfalls

- **`CameraFollow` ignores `enabled = false`:** Coimbra `UpdateEvent` still invokes `HandleUpdate`. Without an `isActiveAndEnabled` early-out, follow overwrites any other driver every frame (map-editor orbit / hologram picks were the discovery case).

## Depends on / Used by

- **Depends on:** [player-control](player-control.md), [inputs](inputs.md)
- **Used by:** [tile](tile.md) (map editor), [interactions-runtime](interactions-runtime.md), entities/humanoid movement

## Related docs

- Design (read-only): [Documents/design/comms.md](../../design/comms.md)
- Architecture (planned): [2026-07_camera-ownership.md](../2026-07_camera-ownership.md)
- Precedent: [2026-07_input-arbitration.md](../2026-07_input-arbitration.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
