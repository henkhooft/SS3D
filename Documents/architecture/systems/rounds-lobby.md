> Code paths: Assets/Scripts/SS3D/Systems/Rounds/, Assets/Scripts/SS3D/Systems/Lobby/
> Entry points: RoundSubSystem, ReadyPlayersSubSystem, RoundSubSystemBase
> Status: shipped
> Verified: 90e26cdc2 — 2026-07-23

# Rounds / lobby

## Overview

Round lifecycle state machine with single-flight `CancellationTokenSource` (prevents double start/stop and embark-during-ending races). States: `Stopped → Preparing → WarmingUp → Ongoing → Ending → Ended`. `PrepareRound` awaits `WorldReadinessSubSystem` `WorldReady` (not a fixed 500 ms delay). Pre-round lobby UI shows ready players and round state.

**Condemned UI:** lobby job-select / ready uGUI — do not extend; replace per [lobby.md](../../design/lobby.md). Round state machine is **not** condemned ([agent-first composition](../2026-07_agent-first-composition.md)).

## Start here

- `Assets/Scripts/SS3D/Systems/Rounds/RoundSubSystem.cs` — concrete round subsystem; `PrepareRound` → `WaitUntilAsync(WorldReady)`
- `Assets/Scripts/SS3D/Systems/Rounds/RoundSubSystemBase.cs` — state machine base with generation-tracked CTS
- `Assets/Scripts/SS3D/Systems/Rounds/ReadyPlayersSubSystem.cs` — player ready tracking; spawns on `Ongoing`
- `Assets/Scripts/SS3D/Systems/WorldReadiness/WorldReadinessSubSystem.cs` — readiness gate for prepare

## Extension points

- Round transitions: extend `RoundSubSystemBase` state handlers and messages in `Messages/`.
- Spawn flow: `SpawnReadyPlayersEvent` after Ongoing (world must already be ready from PrepareRound).

## Depends on / Used by

- **Depends on:** [entities](entities.md), [player-control](player-control.md), [gamemodes-roles-traits](gamemodes-roles-traits.md), [persistence](persistence.md), world readiness (tile/area/electricity/atmos/disposal)
- **Used by:** All in-round gameplay

## Related docs

- Design (read-only): [Documents/design/lobby.md](../../design/lobby.md), [round-config.md](../../design/round-config.md)
- [2026-07_session-world-lifecycle](../2026-07_session-world-lifecycle.md)
- [INDEX.md](../INDEX.md)
