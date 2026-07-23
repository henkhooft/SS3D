> Code paths: Assets/Scripts/SS3D/Systems/PlayerControl/
> Entry points: PlayerSubSystem
> Status: partial
> Verified: 5676d2a2 — 2026-07-18

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
- `Assets/Scripts/SS3D/Systems/PlayerControl/UnauthorizedPlayer.cs` — temporary NOB that broadcasts `UserAuthorizationMessage`
- `Assets/Scripts/SS3D/Systems/Entities/EntitySubSystem.cs` — `TryReclaimEntity` re-links a reconnecting player's `Player` to the body they left behind.

## Extension points

- **Diverges from `networking.md`** (design doc landed after this reconnect flow was built — not yet reconciled):
  - §3 ("reconnecting hands control straight back, no prompt") is what `TryReclaimEntity` does — this part matches.
  - §2's diegetic tell (idle animation slackens/stops while disconnected) is **not implemented** — a disconnected body currently looks identical to an idle player-controlled one; nothing currently signals "unpiloted" on the model itself.
  - §5's server capacity cap (reject a connection past a fixed max player count, with a reason) is **not implemented** — `ProcessPlayerJoin`/`ProcessAuthorizePlayer` accept any connection unconditionally.
  - The `cryogenics.md` §4 grace-period handoff this doc's §3 window is supposed to expire into does not exist yet (`cryogenics` has no architecture/system map — see INDEX.md coverage table) — so today a reconnect window never actually closes; a body left unclaimed sits ownerless until round end, not until some grace period elapses.
- No real authentication (`ProcessAuthorizePlayer` trusts the client-supplied ckey wholesale) — flagged in-code as a TODO, out of scope here.

## Pitfalls

- **`OnClientLoadedStartScenes` must gate on `asServer`:** host also fires the client-side (`asServer=false`) callback before `LoadedStartScenes(true)` is set. Spawning there warns and can create a duplicate UnauthorizedPlayer that later fails despawn ("already deinitializing"). Match FishNet `PlayerSpawner`.
- **Despawn UnauthorizedPlayer carefully:** snapshot `conn.Objects.ToArray()` and skip non-spawned objects before `ServerManager.Despawn` (`IsDeinitializing` is FishNet-internal).

## Depends on / Used by

- **Depends on:** [entities](entities.md), [inputs](inputs.md)
- **Used by:** [interactions-runtime](interactions-runtime.md), [rounds-lobby](rounds-lobby.md)

## Related docs

- Design (read-only): [Documents/design/networking.md](../../design/networking.md) — the connection-lifecycle spec this domain now implements (partially — see Extension points)
- Design (read-only): [Documents/design/main-hud.md](../../design/main-hud.md)
- Observer/ghost reconnection (a different mechanism, for death not network disconnect): [Documents/design/observer.md](../../design/observer.md) §6
- Regression coverage for the disconnect/reconnect ownership flow: `Testing/multiplayer/scenarios/reconnect{,-client}.txt`, see [2026-07_multiplayer-test-harness.md](../2026-07_multiplayer-test-harness.md)
