> Implements: infrastructure — session/scene lifecycle & world readiness; realizes [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) follow-on (a) (code bootstrap + `NetworkSystemsHub`); session/reconnect behavior per [design/networking.md](../design/networking.md) §2–§5
> Touches systems: networking-session, scene-management, application, core-subsystems, tile, area, electricity, atmospherics, rounds-lobby, player-control, persistence, disposal
> Status: planned

# Session & world lifecycle

Server/client init, disconnect handling, and round start are fragile because **prerequisites are
never modeled**. This effort replaces the ad-hoc init/reconnect bandages with two explicit contracts —
a **session lifecycle FSM** and a **world readiness graph** — and lands them on the code-bootstrap
foundation that [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) follow-on
**(a)** and [TECH_DEBT.md](TECH_DEBT.md) §1.7 already name but leave unscheduled.

Sequenced in three phases; **Phase 1 is a standalone shippable slice** (the emergency brake on the
disconnect error storm). Phase 2 removes the init races and host/client divergence. Phase 3 makes 1+2
the only way systems appear.

## 1. Problem & non-goals

One architectural hole with two faces.

**Face A — session/scene lifecycle.** FishNet `DefaultScene` (config in `Boot.unity:281-288`) uses
**offline = Boot**, online = Game. On any disconnect, `DefaultScene.LoadOfflineScene()` reloads Boot as
a *Single* scene while the `NetworkManager` is DontDestroyOnLoad → duplicate managers +
`ApplicationInitializerSubSystem.OnStart()` re-fires the whole
`ApplicationPreInitializing → Initializing → Initialized` chain (the "Boot storm"). Leaked
`ApplicationInitializing` listeners then re-drive `IntroUIHelper`/`SkipIntro` → repeated
`StartNetworkSession` → "Failed to start… already starting/started". There is **no session FSM** today:
`NetworkSessionSubSystem.cs` is imperative start/stop only (fields `NetworkType`, `ServerAddress`,
`Port`; no state, no retry, no backoff). The only mitigation is a **harness-only** Empty-scene redirect
in `AutomationSubSystem` (`RedirectOfflineSceneToEmpty`/`RestoreOfflineScene`, const
`EmptyOfflineScenePath`). Normal play still storms. The one Retry button
(`ServerConnectionView.OnRetryButtonPressed`) is cosmetic — it never re-initiates a connection.

**Face B — world readiness.** Subsystems register in Unity `Awake` (`SubSystem`/`NetworkSubSystem.OnAwake`
→ `SubSystems.Register`) but do real setup later in `OnStartServer`/`OnStartClient` and only become
*functional* after `TileMap.OnMapLoaded`. **Registration ≠ readiness.** Three incompatible readiness
idioms coexist: `IsSetUp` + `OnSystemSetUp` (Area, Electricity), `InitializeWhenMapReady()` UniTask poll
(Atmos, Disposal), and raw `OnMapCreated`/`OnMapLoaded` subscription. Round start is **time-driven**
(`RoundSubSystem.PrepareRound` waits a fixed 500 ms + warmup) and independent of world readiness, so
`SpawnReadyPlayersEvent` can fire on `Ongoing` before flood-fill/atmos init complete. Pure clients
diverge from host: flood-fill and `TileChunk._areaIds` are **server-only and never replicated**, and
lighting RPCs (`RpcAreaLightingStateChanged`) are not `BufferLast`, so a client's `LightPower` cannot
resolve area or lighting state → fixtures dark/stale. This is the bug class being chased on
`cursor/client-light-fixture-sync`.

The Empty-offline / auto-retry work is a correct **bandage**; the proper fix is an explicit lifecycle
that Boot, FishNet, and SubSystems currently only approximate.

**Non-goals.**
- No prediction/rollback/lag-compensation/interpolation ([design/networking.md](../design/networking.md) §9 keeps
  netcode-level concerns out of scope).
- Do **not** encode a full dependency DAG into `SubSystems.Get` — the fix is *don't call `Get` until
  ready*, and make missing-during-`WaitingForServer` silent (as quit already is).
- Do **not** fix every domain race with a one-off SyncVar — SyncVars are for *client view of server
  truth*; readiness is for *when simulation may run*.
- No server browser / matchmaking / capacity-queue ([design/networking.md](../design/networking.md) §5, §9); capacity is a
  plain reject, deferred here.

## 2. Phase 1 — Session lifecycle FSM (standalone shippable slice)

Promote `NetworkSessionSubSystem` (or a small DontDestroyOnLoad owner it holds) into an explicit FSM:

```
Cold → Connecting → Online → Disconnecting → WaitingForServer → Connecting → …
```

Rules that kill the Boot storm:

- **Cold start** may load Boot/Intro **once**. `SkipIntro`/`IntroUIHelper` auto-join fires **only** on
  cold start, never on reconnect.
- **After any join attempt, offline is never Boot.** Redirect FishNet's offline scene to `Empty` (or a
  real menu-shell scene) that does **not** carry an `ApplicationInitializerSubSystem` and therefore
  does **not** fire `ApplicationInitializing`. Promote the harness-only redirect
  (`AutomationSubSystem.RedirectOfflineSceneToEmpty`/`RestoreOfflineScene`/`EmptyOfflineScenePath`,
  via `DefaultScene.SetOfflineScene`) into the session owner so **all** clients use it, not just the
  test harness. Add `Empty`/the menu-shell to `Data/Generated/Scenes.cs` (today only a hardcoded path
  string in `AutomationSubSystem`).
- **Reconnect** only from `WaitingForServer`, via the owner, with manual + exponential backoff
  (2/4/8/16 s). Reuse the harness reconnect logic (`AutomationSubSystem.Reconnect` /
  `StartClientConnectionFromSettings` / `IsClientFullyStopped`) as the shared implementation. Wire the
  currently-cosmetic `ServerConnectionView` Retry button to the owner's reconnect. Never reconnect via
  Intro/`SkipIntro`.
- **Online** loads Game once; **Disconnecting** tears Game down cleanly before entering
  `WaitingForServer`.
- `ApplicationInitializing` listeners are one-shot / owned via `Actor.AddHandle` so Boot cannot
  multiply them — audit subscribers (`SceneSubSystem`, `NetworkSessionSubSystem` server-only,
  `CommandLineArgsSubSystem`, and the `Tween`/`Assets`/`LocalSave` initialization triggers). Once
  offline ≠ Boot after the first join, the storm's root cause is removed; keep the `AddHandle`
  discipline as belt-and-suspenders.

The existing bandages — the Empty arm, the `AutomationSubSystem` `_scriptStarted`/reconnect guards, the
`ServerConnectionView` retry — are unnamed pieces of this FSM. Phase 1 **names the states** and makes
the redirect universal rather than harness-only. Reconnect-to-body already works and is unchanged:
`PlayerSubSystem.ProcessAuthorizePlayer` → `EntitySubSystem.TryReclaimEntity` implements
[design/networking.md](../design/networking.md) §3 ("reconnecting hands control straight back").

**Key files:** `Networking/NetworkSessionSubSystem.cs`, `Networking/ServerConnectionView.cs`,
`Networking/NetworkSessionStartedEvent.cs`, `SceneManagement/SceneSubSystem.cs`,
`Systems/Intro/IntroUIHelper.cs`, `Systems/Testing/AutomationSubSystem.cs` (band-aid collapses into
the owner), `Data/Generated/Scenes.cs`, and the FishNet `DefaultScene` config in
`Assets/Content/Scenes/Boot.unity`.

## 3. Phase 2 — World readiness graph

Separate **registered** from **ready**. Introduce one contract to replace the three idioms.

- `IWorldReady` / `WhenReady` on world subsystems — exposed **both** as an event and as a UniTask
  awaitable — plus a small `WorldReadiness` coordinator that models phases:

  ```
  TileMap loaded  →  Areas flooded  →  Electricity ready
                  ↘  Atmos ready
  Persistence OnAfterRestore  →  (re)fire the same gates
  ```

- Base the "tile ready" gate on `TileMap.OnMapLoaded` / end-of-restore (`AreaSubSystem.EndDeferredAreaFlood`,
  `PersistenceSubSystem.OnAfterRestore`), **not** `OnMapCreated`. `OnMapCreated` fires when the map
  *object* exists but tiles are not placed — the two-phase gap Atmos/Disposal currently trip on by
  waiting on `CurrentMap != null`.
- Migrate `AreaSubSystem`/`ElectricitySubSystem` off `IsSetUp` + `OnSystemSetUp`, and
  `AtmosSubSystem.InitializeWhenMapReady()` / `DisposalSubSystem` off UniTask polling, onto the
  contract. Each declares its prerequisites (`DependsOn`: Electricity → Area, Atmos → Tile, Area →
  Tile-loaded). Consumers use `TryGet` + ready or subscribe to `WhenReady` — never `Get<T>()` in
  `Update`/`FixedUpdate` that Errors when the target is missing.
- **Gate round start / embark on readiness.** `RoundSubSystem.PrepareRound` awaits world-ready instead
  of the fixed 500 ms; `ReadyPlayersSubSystem`/`EntitySubSystem` spawn only after the ready epoch. This
  closes "spawn before flood-fill/atmos".
- **Client parity contract (umbrella — does not absorb the branch fix).** Pure clients never run
  flood-fill; today area ids live only in server `TileChunk._areaIds` and lighting RPCs are not
  buffered. Contract: the server publishes a "world epoch N ready" signal plus **buffered** replication
  of the area-id → tile mapping (or per-area/fixture lighting state) so a client's `LightPower` resolves
  area + lighting **without** host flood-fill and regardless of join timing. The
  `cursor/client-light-fixture-sync` SyncVar/buffered-RPC work is the **first concrete instance** of
  this contract — it ships independently on that branch; this doc is the umbrella that keeps future
  client-parity work consistent, not a rewrite of it.

**Key files:** `Systems/Tile/{TileSubSystem,TileMap,TileChunk}.cs`;
`Systems/Area/{AreaSubSystem,AreaFloodFillService,AreaLightingStateDeriver,AreaFloorVisualCache}.cs`;
`Systems/Electricity/{ElectricitySubSystem,LightPower}.cs`; `Systems/Atmospherics/AtmosSubSystem.cs`;
`Systems/Furniture/Disposal/DisposalSubSystem.cs`; `Systems/Persistence/PersistenceSubSystem.cs`;
`Systems/Rounds/{RoundSubSystem,ReadyPlayersSubSystem}.cs`; `Systems/Entities/EntitySubSystem.cs`; and
a new `IWorldReady` + `WorldReadiness` under `Core/` (or `Systems/WorldReadiness/`).

## 4. Phase 3 — Thin scenes + code bootstrap + `NetworkSystemsHub`

Realizes [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) follow-on **(a)**;
resolves [TECH_DEBT.md](TECH_DEBT.md) §1.7 and pays down §1.12.

- One code **bootstrap** builds process-wide services (the Phase 1 session owner, logging, UI shell) at
  cold start.
- One networked **`NetworkSystemsHub`** — a single NetworkObject, not one-per-system in Boot — plus the
  Game load builds **world** SubSystems in **dependency order** (Phase 2's `DependsOn`) when Online.
  Unloading Online unregisters world systems in **reverse** dependency order so `WaitingForServer` is
  never a half-registered Game.
- Empties `Boot.unity` (5 persistent systems) and `Game.unity` (~29 systems) of per-system GameObjects;
  folds the `RuntimeInitializeOnLoadMethod` self-bootstraps (`ScreenEffectsSubSystem`,
  `AutomationSubSystem`) and the manual Comms/`Human.prefab` wiring into the bootstrap — collapsing the
  three competing bootstrap styles into one.
- Relax the `SubSystems.Get<T>()` `FindObjectOfType`-or-Error fallback ([TECH_DEBT.md](TECH_DEBT.md)
  §1.12) toward `TryGet` + ready; make missing-during-`WaitingForServer` silent, the way teardown
  already suppresses via `_isQuitting`.

**Key files:** `Core/Subsystems.cs`, `Core/Behaviours/{SubSystem,NetworkSubSystem}.cs`,
`Application/ApplicationInitializerSubSystem.cs`, `Assets/Content/Scenes/{Boot,Game}.unity`, and new
`SystemsBootstrap` + `NetworkSystemsHub` types.

## 5. Sequencing & acceptance criteria

Build order — each phase is independently valuable and unblocks the next:

1. **Phase 1** — session FSM + never re-enter Boot after the first join attempt. Stops the
   ~1000-error storm class.
2. **Phase 2** — world-ready signals for tile → area → electricity/atmos. Stops "subsystem exists but
   map null" and host/client divergence.
3. **Phase 3** — code bootstrap + `NetworkSystemsHub`. Makes Phases 1+2 the only way systems appear;
   kills [TECH_DEBT.md](TECH_DEBT.md) §1.7.

Per-phase acceptance:

- **Phase 1** — a real (non-harness) client disconnect never reloads Boot; the client sits in the
  waiting/menu scene and reconnects via backoff, restoring control to its body; no
  "already starting/started" or duplicate-manager errors in `unity.log`.
- **Phase 2** — no spawn or tick-driven consumer runs before its prerequisite's ready epoch; a
  late-joining pure client lights fixtures correctly (area + lighting resolved without host flood-fill);
  persistence restore re-fires readiness cleanly.
- **Phase 3** — no gameplay subsystem is scene-placed in Boot/Game, and no
  `RuntimeInitializeOnLoadMethod` bootstrap remains; adding a new system requires no scene YAML edit.

Verification per phase (when built, not in this planning pass): Editor Play Mode host + client; the
multiplayer smoke harness `Testing/multiplayer/run_smoketest.sh` with the `reconnect` /
`reconnect-client` scenarios (`Testing/multiplayer/scenarios/reconnect{,-client}.txt`); the EditMode
suite via `.github/workflows/editmodetestrunner.yml`. Run the `update-system-docs` skill after each
phase ships to sync the affected system maps, INDEX, and this doc's `Status`.

## 6. Related docs

- Composition target: [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) (follow-on (a))
- Debt register: [TECH_DEBT.md](TECH_DEBT.md) §1.7 (bootstrap styles), §1.12 (`Get` FindObject fallback)
- Design (read-only): [design/networking.md](../design/networking.md) §2–§5 (disconnect/reconnect/capacity)
- System maps: [networking-session](systems/networking-session.md), [scene-management](systems/scene-management.md),
  [application](systems/application.md), [core-subsystems](systems/core-subsystems.md),
  [player-control](systems/player-control.md), [tile](systems/tile.md), [area](systems/area.md),
  [electricity](systems/electricity.md), [atmospherics](systems/atmospherics.md),
  [persistence](systems/persistence.md), [rounds-lobby](systems/rounds-lobby.md)
