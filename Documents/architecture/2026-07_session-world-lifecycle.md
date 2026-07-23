> Implements: infrastructure — session/scene lifecycle & world readiness; realizes [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) follow-on (a) (code bootstrap + `NetworkSystemsHub`); session/reconnect behavior per [design/networking.md](../design/networking.md) §2–§5
> Touches systems: networking-session, scene-management, application, core-subsystems, tile, area, electricity, atmospherics, rounds-lobby, player-control, persistence, disposal
> Status: in-progress (Phase 1 FSM + Empty codegen, Phase 2b world-readiness graph, Phase 3 scaffolding shipped; Boot/Game still hold most scene SubSystems — hub dual-runs until Editor migration empties them)

# Session & world lifecycle

Server/client init, disconnect handling, and round start are fragile because **prerequisites are
never modeled**. This effort replaces the ad-hoc init/reconnect bandages with two explicit contracts —
a **session lifecycle FSM** and a **world readiness graph** — and lands them on the code-bootstrap
foundation that [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) follow-on
**(a)** and [TECH_DEBT.md](TECH_DEBT.md) §1.7 already name but leave unscheduled.

Sequenced in three phases; **Phase 1 is a standalone shippable slice** (the emergency brake on the
disconnect error storm). Phase 2 removes the init races and host/client divergence. Phase 3 makes 1+2
the only way systems appear.

**Update 2026-07-23 (implementation pass):** Named `SessionState` on `ClientConnectionRecovery`;
`Scenes.Empty` / `Scenes.EmptyPath`; Automation no longer restores Boot offline; `IWorldReady` +
`WorldReadinessSubSystem`; Area/Electricity/Atmos/Disposal migrated; `PrepareRound` awaits
`WorldReady`; `SystemsBootstrap` + `NetworkSystemsHub` Resources prefab + spawn on Online;
`SubSystems.Get` silent during `WaitingForServer`; VisionSystem removed from Game (DDOL only).
Full Boot/Game empty (Phase 3h) still open — use `SS3D/Bootstrap/*` Editor menus.

## 1. Problem & non-goals

One architectural hole with two faces.

**Face A — session/scene lifecycle.** FishNet `DefaultScene` (config in `Boot.unity:281-288`) uses
**offline = Boot**, online = Game. On any disconnect, `DefaultScene.LoadOfflineScene()` reloads Boot as
a *Single* scene while the `NetworkManager` is DontDestroyOnLoad → duplicate managers +
`ApplicationInitializerSubSystem.OnStart()` re-fires the whole
`ApplicationPreInitializing → Initializing → Initialized` chain (the "Boot storm"). Leaked
`ApplicationInitializing` listeners then re-drive `IntroUIHelper`/`SkipIntro` → repeated
`StartNetworkSession` → "Failed to start… already starting/started".

**Update 2026-07-23 — the storm symptom is now fixed, landed independently of this doc via PR #36
(`cursor/client-light-fixture-sync` → `develop`).** New `Networking/ClientConnectionRecovery.cs` — a
DontDestroyOnLoad component on the `NetworkManager` — arms `Empty.unity` as the FishNet offline scene
after the **first successful connection**, so a later disconnect no longer reloads Boot; it also owns
an OnGUI Retry/Quit fallback for the case where Boot (and its UI) is already gone. Re-entrant
`StartNetworkSession()` calls are now guarded by `NetworkSessionSubSystem.CanStartNetworkSession`
(skips while the client is Starting/Started/Stopping — the exact condition that used to storm). The
`ServerConnectionView` Retry button (for the pre-first-connect / Intro-still-loaded case) now really
calls `StartNetworkSession()` again instead of only replaying the loading animation. The previously
harness-only redirect (`AutomationSubSystem.RedirectOfflineSceneToEmpty`) is now belt-and-suspenders on
top of the universal fix rather than the only mitigation. See
[networking-session.md](systems/networking-session.md) Pitfalls for the updated account.

**What this fix is not:** an explicit named-state session FSM (`Cold → Connecting → Online → …`). It's
a targeted pair of guards plus a small recovery component — the *behavioral contract* Phase 1 below
asks for is met (offline is never Boot after first join; retry actually retries), but there is still
no single state enum a future feature could branch on. Formalizing that remains optional polish, not
required follow-up — see §2.

**Face B — world readiness.** Subsystems register in Unity `Awake` (`SubSystem`/`NetworkSubSystem.OnAwake`
→ `SubSystems.Register`) but do real setup later in `OnStartServer`/`OnStartClient` and only become
*functional* after `TileMap.OnMapLoaded`. **Registration ≠ readiness.** Three incompatible readiness
idioms coexist: `IsSetUp` + `OnSystemSetUp` (Area, Electricity), `InitializeWhenMapReady()` UniTask poll
(Atmos, Disposal), and raw `OnMapCreated`/`OnMapLoaded` subscription. Round start is **time-driven**
(`RoundSubSystem.PrepareRound` waits a fixed 500 ms + warmup) and independent of world readiness, so
`SpawnReadyPlayersEvent` can fire on `Ongoing` before flood-fill/atmos init complete — this half of
Face B is **still unaddressed** (see §3b).

The specific client/host divergence this doc originally called out — flood-fill and `TileChunk._areaIds`
being server-only and never replicated, plus non-`BufferLast` lighting RPCs, so a client's `LightPower`
couldn't resolve area or lighting state — was the bug class `cursor/client-light-fixture-sync` was
chasing. **It shipped 2026-07-23** (PR #36); see §3a for what landed and why it validates this doc's
client-parity contract without needing the general readiness graph.

The Empty-offline / auto-retry work started as a harness-only bandage and has since been promoted to
the universal fix for Face A (see the 2026-07-23 update above). The remaining proper fix — an explicit
lifecycle that Boot, FishNet, and SubSystems currently only approximate — is now scoped to Face B
(world readiness, §3) plus the optional FSM formalization named in §2.

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

**Status: shipped (pragmatic form), 2026-07-23, via PR #36.** The behavioral contract below is met —
offline is never Boot after the first join, and reconnect/retry actually reconnects — via
`ClientConnectionRecovery.cs` + `NetworkSessionSubSystem.CanStartNetworkSession` +
`ServerConnectionView`'s now-functional Retry, described in the Face A update above. What follows is
the **original target shape** (an explicit named-state FSM); it is **not required** to get the
acceptance criteria in §5 — those are already met. Treat this section as optional future
consolidation (e.g. if a later feature needs to branch on "are we mid-reconnect" as a first-class
state rather than inferring it from `SubSystems.TryGet<NetworkSessionSubSystem>()` presence, the way
`ClientConnectionRecovery.HandleClientConnectionState` currently does).

Promote `NetworkSessionSubSystem` (or a small DontDestroyOnLoad owner it holds) into an explicit FSM:

```
Cold → Connecting → Online → Disconnecting → WaitingForServer → Connecting → …
```

Rules that kill the Boot storm:

- **Cold start** may load Boot/Intro **once**. `SkipIntro`/`IntroUIHelper` auto-join fires **only** on
  cold start, never on reconnect.
- **After any join attempt, offline is never Boot.** **Done** — `ClientConnectionRecovery` arms
  `Empty.unity` as FishNet's offline scene after the first Started connection (both client and server
  paths), so `ApplicationInitializerSubSystem` never re-fires on disconnect. Remaining formalization
  only: give `Empty` a real codegen entry in `Data/Generated/Scenes.cs` (today it's a hardcoded path
  string in both `ClientConnectionRecovery` and `AutomationSubSystem`) and fold the const into one
  place.
- **Reconnect** only after Boot is gone, via a manual Retry button. **Done in pragmatic form** —
  `ClientConnectionRecovery`'s OnGUI Retry re-starts the connection directly from `NetworkSettings`
  when `NetworkSessionSubSystem` isn't around (Boot unloaded), or calls `session.StartNetworkSession()`
  when it still is; `ServerConnectionView`'s Retry does the latter for the pre-first-connect case.
  **Not done:** exponential backoff (today's retry is a single manual button press, no automatic
  2/4/8/16 s escalation) — still worth adding if disconnects prove to cluster around transient network
  blips. Never reconnects via Intro/`SkipIntro` — confirmed unchanged.
- **Online** loads Game once; **Disconnecting** tears Game down cleanly before entering
  `WaitingForServer`.
- `ApplicationInitializing` listeners are one-shot / owned via `Actor.AddHandle` so Boot cannot
  multiply them — audit subscribers (`SceneSubSystem`, `NetworkSessionSubSystem` server-only,
  `CommandLineArgsSubSystem`, and the `Tween`/`Assets`/`LocalSave` initialization triggers). Once
  offline ≠ Boot after the first join, the storm's root cause is removed; keep the `AddHandle`
  discipline as belt-and-suspenders.

`ClientConnectionRecovery`, `NetworkSessionSubSystem.CanStartNetworkSession`, and
`ServerConnectionView`'s retry are unnamed pieces of an FSM — they satisfy the behavioral contract
without ever naming a state. Formalizing them into the explicit enum above remains optional; nothing
in §5's acceptance criteria requires it. Reconnect-to-body already works and is unchanged:
`PlayerSubSystem.ProcessAuthorizePlayer` → `EntitySubSystem.TryReclaimEntity` implements
[design/networking.md](../design/networking.md) §3 ("reconnecting hands control straight back").

**Key files (as shipped):** `Networking/ClientConnectionRecovery.cs`,
`Networking/NetworkSessionSubSystem.cs`, `Networking/ServerConnectionView.cs`,
`Networking/NetworkSessionStartedEvent.cs`, `SceneManagement/SceneSubSystem.cs`,
`Systems/Intro/IntroUIHelper.cs`, `Systems/Testing/AutomationSubSystem.cs` (belt-and-suspenders
redirect only, no longer the sole mitigation), `Data/Generated/Scenes.cs` (still missing `Empty`),
and the FishNet `DefaultScene` config in `Assets/Content/Scenes/Boot.unity`.

## 3. Phase 2 — World readiness graph

Separate **registered** from **ready**. Two distinct pieces below: the **client-parity contract**,
which has a **shipped, validated instance** (§3a), and the **general world-readiness graph**, which
remains open work (§3b).

### 3a. Client parity contract — shipped instance (PR #36)

Pure clients never run flood-fill; area ids used to live only in server `TileChunk._areaIds` and
lighting RPCs were not buffered, so a client's `LightPower` could not resolve area or lighting state.
**Fixed 2026-07-23**, independently of this doc, on `cursor/client-light-fixture-sync`:

- `LightPower._fixtureVisual` became a server-authoritative **SyncVar** — FishNet auto-replicates
  SyncVar values to late joiners, which is exactly the "buffered" property this contract needs; the
  server computes Off/Normal/Emergency and clients only apply it (never re-derive from
  `AreaSubSystem.IsSetUp`, which stays `false` on pure clients).
- `AreaSubSystem`'s per-area lighting RPC was replaced with a **BufferLast full-snapshot**
  `RpcSyncAreaLighting` (the old non-buffered per-area RPC only retained state for the *last* area
  touched — a late joiner or a client that missed one area's transition never caught up).
- New `AreaDeviceTileResolver` + `AreaFloorVisualCache.TryGetAreaIdForWorldGrid` let a client resolve
  "which area is this device in" from its own local floor-cache snapshot, without needing the host's
  live flood-fill registry at all.

This validates the contract shape this doc asked for (buffered server truth + a client-side resolver
that doesn't depend on host-only state) with a real, narrower implementation than a general
"world epoch N" broadcast — worth keeping as the reference pattern for any future domain that hits the
same host/client divergence. Detail: [area.md](systems/area.md) Pitfalls ("Client fixtures stay stuck
on/off"), [electricity.md](systems/electricity.md) Pitfalls ("Client light fixtures ignore APC / wall-switch
toggles").

**Key files (as shipped):** `Systems/Electricity/LightPower.cs`, `Systems/Area/AreaSubSystem.cs`,
`Systems/Area/AreaDeviceTileResolver.cs`, `Systems/Area/AreaFloorVisualCache.cs`.

### 3b. General world-readiness graph — still open

Everything below is **unaffected by PR #36** and remains the real Phase 2 work. Introduce one contract
to replace the three readiness idioms:

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
- **Gate round start / embark on readiness.** `RoundSubSystem.PrepareRound` still awaits a fixed 500 ms
  (unchanged by PR #36) — this should await world-ready instead; `ReadyPlayersSubSystem`/
  `EntitySubSystem` should spawn only after the ready epoch. This closes "spawn before flood-fill/atmos".

**Key files:** `Systems/Tile/{TileSubSystem,TileMap,TileChunk}.cs`;
`Systems/Area/{AreaSubSystem,AreaFloodFillService,AreaLightingStateDeriver,AreaFloorVisualCache}.cs`;
`Systems/Electricity/ElectricitySubSystem.cs`; `Systems/Atmospherics/AtmosSubSystem.cs`;
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
   ~1000-error storm class. **Shipped** 2026-07-23 (pragmatic form, PR #36).
2. **Phase 2** — world-ready signals for tile → area → electricity/atmos. Stops "subsystem exists but
   map null" and host/client divergence. **§3a (client-parity) shipped** 2026-07-23 (PR #36); **§3b
   (general readiness graph) still open.**
3. **Phase 3** — code bootstrap + `NetworkSystemsHub`. Makes Phases 1+2 the only way systems appear;
   kills [TECH_DEBT.md](TECH_DEBT.md) §1.7. **Open.**

Per-phase acceptance:

- **Phase 1 — met.** A real (non-harness) client disconnect no longer reloads Boot
  (`ClientConnectionRecovery` arms `Empty.unity` after first connect); a disconnected client can
  retry (OnGUI Retry / `ServerConnectionView` Retry) and reconnect, restoring control to its body; no
  "already starting/started" storm (`NetworkSessionSubSystem.CanStartNetworkSession` guards re-entry).
  Not automatic/backoff-driven — retry is still a manual button press.
- **Phase 2, §3a — met.** A late-joining pure client lights fixtures correctly: area resolved via
  `AreaDeviceTileResolver`/`AreaFloorVisualCache`, lit state via a SyncVar + BufferLast snapshot, no
  host flood-fill dependency.
- **Phase 2, §3b — open.** No spawn or tick-driven consumer runs before its prerequisite's ready
  epoch; persistence restore re-fires readiness cleanly. `RoundSubSystem.PrepareRound` still uses a
  fixed 500 ms wait, not a readiness gate.
- **Phase 3 — open.** No gameplay subsystem is scene-placed in Boot/Game, and no
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
