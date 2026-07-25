> Code paths: Assets/Scripts/SS3D/Systems/Comms/, Assets/Scripts/SS3D/Systems/Screens/, Assets/Content/Data/Comms/Channels/, Assets/Content/Systems/UI/Comms/
> Entry points: CommsSubSystem, LocalSpeechBubbleController, CommsFeedController, PlayerCameraSubSystem, CameraSubSystem, CameraFollow
> Status: partial
> Verified: 4bc2ae93d — 2026-07-25

# Chat / audio / screens

## Overview

In-game comms hub, local-speech UI, non-diegetic radio/announcement feed, and camera/screen
controllers. Audio has its own map — [audio](audio.md). Camera pose is still multi-writer — see
[camera ownership](../2026-07_camera-ownership.md).

**Comms hub:** `CommsSubSystem` owns local speech (proximity ObserversRpc) and non-positional radio /
announcements (global `CommsMessage` broadcast). Legacy `ChatSubSystem` / `Engine.Chat` purged;
channel SOs live under `Assets/Content/Data/Comms/Channels/` (`CommsChannel` / `CommsChannels`).

Local speech: weighted chips + T-compose (Enter speak / Shift+Enter whisper / Ctrl+Enter shout).
**Tab / Shift+Tab** cycles Local → writable radio channels; radio commit uses `CmdSendRadio` (no head
bubble). Feed UI on `UiShell` HUD: left radio stack + top ALL-STATION banner.

## Start here

- `CommsSubSystem.cs` — local + radio/announce hub; `SendAnnouncement`; `OnLocalSpeechReceived` /
  `OnCommsMessageReceived`; transcript `Logs/Comms.txt` on server.
- `LocalSpeechEmitter` — `CmdSpeak` / `CmdSendRadio` on the speaking Entity.
- `LocalSpeechBubbleController` + `LocalSpeechBubbleView` — head chips + compose (own UIDocument).
- `CommsFeedController` + `CommsFeedView` — attaches to `UiLayer.Hud`; radio left, announce top.
- Channel settings: `Assets/Settings/CommsChannelsSettings.asset`.
- Screens: `PlayerCameraSubSystem`, `CameraSubSystem`, `CameraFollow` (guard `isActiveAndEnabled`).

## Pitfalls

- **Tab-in-compose** replaces design §6 channel radial for this slice — do not add typed `;` prefixes.
- **Announcements:** all `Announcement`-kind traffic uses the top banner (no routine→feed split yet).
- **Headset traits** on `CommsChannel` are data-only until MVP2 gating — Tab lists all writable radio.
- **Do not resurrect UGUI always-on chat.**
- **Speech bubbles invisible / NaN size:** `EnsureOverlay` must require `root.panel != null` and tear
  down on disable (UIDocument rebuild orphans).
- **Compose:** `TextEntry` + `InputTextEntryScope` + TrickleDown Enter/Tab; draft focus lock; measure
  draft width via Label proxy; no USS `translate: -50%` on draft or announce banner.
- **UITK:** do not combine `border-radius` + `overflow: hidden` on the same element.
- **Occlusion:** shared `SS3D.Utils.LineOfSight` — no private raycast in `LocalSpeechListener`.
- **`CameraFollow` ignores `enabled = false`:** Coimbra `UpdateEvent` needs `isActiveAndEnabled` early-out.
- Radio/Announcement **head-chip** CSS is draft/preview only; live path is `CommsFeedView`.

## Extension points

- Mode-pip fading hints (comms.md §5); whisper presence-while-typing for other viewers.
- PDA log / history ([comms.md](../../design/comms.md) §9).
- Channel radial; headset/ID gating (MVP2 S4).
- Migrate local-speech overlay onto UiShell (feed already on Hud).

## Depends on / Used by

- **Depends on:** [player-control](player-control.md), [inputs](inputs.md), [ui-shell](ui-shell.md)
- **Used by:** [rounds-lobby](rounds-lobby.md) (announcements), [tile](tile.md) (map editor camera),
  [interactions-runtime](interactions-runtime.md)

## Related docs

- Design: [Documents/design/comms.md](../../design/comms.md)
- Effort: [2026-07_comms-non-diegetic-feed.md](../2026-07_comms-non-diegetic-feed.md)
- Plan: [comms_non_diegetic_feed.plan.md](../../plans/comms_non_diegetic_feed.plan.md)
- [2026-07_agent-first-composition](../2026-07_agent-first-composition.md)
- [2026-07_input-arbitration](../2026-07_input-arbitration.md)
- [2026-07_camera-ownership.md](../2026-07_camera-ownership.md)
