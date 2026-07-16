> Code paths: Assets/Scripts/SS3D/Systems/PlayerControl/
> Entry points: PlayerSubSystem
> Status: partial

# Player control

## Overview

Player subsystem — connect/authorize/disconnect flow for `Player` (the persistent
per-ckey identity object, distinct from the `Entity` body it controls), input routing,
round join ordering.

Connect/disconnect lifecycle, server-side:

1. `ServerManager.OnRemoteConnectionState` fires nothing on connect; `SceneManager.OnClientLoadedStartScenes` → `HandleClientLoadedStartScenes` → `ProcessPlayerJoin` spawns a temporary `UnauthorizedPlayer` for the new connection.
2. The client's `UnauthorizedPlayer.Setup()` immediately broadcasts `UserAuthorizationMessage` with its ckey (no real login — see TODO in `ProcessAuthorizePlayer`).
3. `ProcessAuthorizePlayer` finds-or-creates the `_serverPlayers[ckey]` `Player` object, gives the connection ownership of it, adds it to `_onlinePlayers`, **reconnects it to any existing body** via `EntitySubSystem.TryReclaimEntity` if `IsPlayerSpawned(player)` is true (i.e. they disconnected mid-round and are rejoining), then despawns the `UnauthorizedPlayer`.
4. `ServerManager.OnRemoteConnectionState` → `Stopped` → `ProcessPlayerDisconnect` calls `RemoveOwnership()` on every `NetworkObject` the connection owns (the `Player`, and separately the controlled `Entity`/body if one was spawned) and removes the ckey from `_onlinePlayers`. The body itself is **not** despawned here — it's left ownerless in the world until either the owning player reconnects (step 3) or the round ends (`EntitySubSystem.DestroySpawnedPlayers`).

## Start here

- `Assets/Scripts/SS3D/Systems/PlayerControl/PlayerSubSystem.cs` — player subsystem; `ProcessPlayerJoin`/`ProcessAuthorizePlayer`/`ProcessPlayerDisconnect` own the connect/disconnect lifecycle above.
- `Assets/Scripts/SS3D/Systems/Entities/EntitySubSystem.cs` — `TryReclaimEntity` re-links a reconnecting player's `Player` to the body they left behind.

## Extension points

- Reconnection currently only restores ownership/control of the existing body (`GiveOwnership` + `SetFirstObject` + re-fires the same `RpcInvokeClientSpawned` a fresh spawn uses). It does not model a "disconnected" state on the body in the meantime (no ragdoll, no SSD-style visual indicator, no timeout that eventually kills/despawns an abandoned body) — bodies just sit inert, fully simulated, until reclaimed or the round ends. Any of that is a deliberate follow-up, not an oversight this pass tried to close.
- No real authentication (`ProcessAuthorizePlayer` trusts the client-supplied ckey wholesale) — flagged in-code as a TODO, out of scope here.

## Depends on / Used by

- **Depends on:** [entities](entities.md), [inputs](inputs.md)
- **Used by:** [interactions-runtime](interactions-runtime.md), [rounds-lobby](rounds-lobby.md)

## Related docs

- Design (read-only): [Documents/design/main-hud.md](../../design/main-hud.md)
- Observer/ghost reconnection (a different mechanism, for death not network disconnect): [Documents/design/observer.md](../../design/observer.md) §6
