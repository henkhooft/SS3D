> Code paths: Assets/Scripts/SS3D/Systems/Rounds/, Assets/Scripts/SS3D/Systems/Lobby/, Assets/Scripts/SS3D/UI/Lobby/
> Entry points: RoundSubSystem, ReadyPlayersSubSystem, RoundSubSystemBase, LobbyUiSubSystem
> Status: partial
> Verified: 473f62eea — 2026-08-13

# Rounds / lobby

## Overview

Round lifecycle state machine with single-flight `CancellationTokenSource` (prevents double start/stop and embark-during-ending races). States: `Stopped → Preparing → WarmingUp → Ongoing → Ending → Ended`. `PrepareRound` awaits `WorldReadinessSubSystem` `WorldReady` (not a fixed 500 ms delay).

**UITK lobby (in progress):** Phase A shell + Phase B Character Creator visual + Phase E1 live booth
preview on `UiLayer.Modal` — [2026-08_lobby-uitk-redesign](../2026-08_lobby-uitk-redesign.md).
Ready/round APIs and appearance apply (E2) still pending.

**Condemned UI:** lobby job-select / ready uGUI (`Systems/Lobby/UI`, `LobbyCanvas`) — do not extend; replace per [lobby.md](../../design/lobby.md). Round state machine is **not** condemned ([agent-first composition](../2026-07_agent-first-composition.md)).

## Start here

- `Assets/Scripts/SS3D/UI/Lobby/LobbyUiSubSystem.cs` — UITK shell + Character Creator host; attaches into Modal
- `Assets/Scripts/SS3D/UI/Lobby/LobbyShellView.cs` — full-screen lobby chrome (tabs + sidebar)
- `Assets/Scripts/SS3D/UI/Lobby/CharacterCreatorView.cs` — guided-steps Character Creator
- `Assets/Scripts/SS3D/Systems/Entities/Character/CharacterPreviewBooth.cs` — off-map RT booth (layer 22, own lights)
- `Assets/Scripts/SS3D/Systems/Rounds/RoundSubSystem.cs` — concrete round subsystem; `PrepareRound` →
  `WaitUntilAsync(WorldReady)`; on Ongoing fires welcome via `CommsSubSystem.SendAnnouncement`
  (`StationWelcome` follow-up clip)
- `Assets/Scripts/SS3D/Systems/Rounds/RoundSubSystemBase.cs` — state machine base with generation-tracked CTS
- `Assets/Scripts/SS3D/Systems/Rounds/ReadyPlayersSubSystem.cs` — player ready tracking; spawns on `Ongoing`
- `Assets/Scripts/SS3D/Systems/WorldReadiness/WorldReadinessSubSystem.cs` — readiness gate for prepare

## Extension points

- Round transitions: extend `RoundSubSystemBase` state handlers and messages in `Messages/`.
- Spawn flow: `SpawnReadyPlayersEvent` after Ongoing (world must already be ready from PrepareRound).
- Lobby UITK: Phase C+ wires Ready / round / jobs into `LobbyShellView`; Character Creator is a separate Modal screen (Phase B/E).

## Pitfalls

- **Phase A double UI:** UITK shell shows on attach while condemned `LobbyCanvas` may still be active. Disable the old canvas in the Hierarchy when visually checking the new shell.
- **Jobs prefs:** shell uses H/M/L/N chips (attached design), not drag-ranked lists (`lobby.md` §3) — recorded on the architecture effort.
- **Preview booth:** live preview uses `CharacterPreviewBooth` on unused layer **22** with its own camera/lights/RT — do **not** bind `LobbyCameraRenderTexture` or the `StaticWorldObjects` customizer camera. Dispose the booth on host destroy; deactivate (don’t destroy) on Return to avoid respawn hitch.
- **E1 vs E2:** Save stores a local name + body morph draft; hair/species do not yet apply. Body sliders map to Human.fbx blend shapes via `HumanoidMorphApplier` (Height is root scale — there is no Height shape key).

## Depends on / Used by

- **Depends on:** [entities](entities.md), [player-control](player-control.md),
  [gamemodes-roles-traits](gamemodes-roles-traits.md), [persistence](persistence.md),
  [chat-audio-screens](chat-audio-screens.md) (round-start announce), [ui-shell](ui-shell.md),
  world readiness (tile/area/electricity/atmos/disposal)
- **Used by:** All in-round gameplay

## Related docs

- Design (read-only): [Documents/design/lobby.md](../../design/lobby.md), [round-config.md](../../design/round-config.md)
- Effort: [2026-08_lobby-uitk-redesign](../2026-08_lobby-uitk-redesign.md)
- [2026-07_session-world-lifecycle](../2026-07_session-world-lifecycle.md)
- [2026-07_comms-non-diegetic-feed](../2026-07_comms-non-diegetic-feed.md)
- [INDEX.md](../INDEX.md)
