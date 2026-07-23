> Implements: infrastructure — session/scene lifecycle & world readiness; realizes [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) follow-on (a) (code bootstrap + `NetworkSystemsHub`); session/reconnect behavior per [design/networking.md](../design/networking.md) §2–§5
> Touches systems: networking-session, scene-management, application, core-subsystems, tile, area, electricity, atmospherics, rounds-lobby, player-control, persistence, disposal
> Status: in-progress (Phase 1 FSM + Empty codegen, Phase 2b world-readiness graph, Phase 3 scaffolding shipped; Boot/Game still hold most scene SubSystems — hub dual-runs until Editor migration empties them)

# Session & world lifecycle

Server/client init, disconnect handling, and round start are fragile because **prerequisites are
never modeled**. This effort replaces the ad-hoc init/reconnect bandages with two explicit contracts —
a **session lifecycle FSM** and a **world readiness graph** — and lands them on the code-bootstrap
foundation that [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) follow-on
**(a)** and [TECH_DEBT.md](TECH_DEBT.md) §1.7 name. Phase 3 scaffolding for that foundation is
shipped; emptying Boot/Game (Phase 3h) is still open.

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

**Update (same day, implementation branch):** Phase 1 also landed the named `SessionState` FSM on
`ClientConnectionRecovery`, `Scenes.Empty` / `Scenes.EmptyPath` codegen, Cold-only Intro auto-join, and
silent `SubSystems.Get` while `WaitingForServer`. See §2.

**Face B — world readiness.** Subsystems register in Unity `Awake` (`SubSystem`/`NetworkSubSystem.OnAwake`
→ `SubSystems.Register`) but do real setup later in `OnStartServer`/`OnStartClient` and only become
*functional* after the map is actually loaded. **Registration ≠ readiness.** Historically three
incompatible idioms coexisted (`IsSetUp`/`OnSystemSetUp`, UniTask polls on `CurrentMap != null`, raw
`OnMapCreated`/`OnMapLoaded`). Round start was time-driven (`PrepareRound` fixed 500 ms), so
`SpawnReadyPlayersEvent` could fire before flood-fill/atmos init. **§3b shipped** the general
`IWorldReady` / `WorldReadinessSubSystem` graph and gates `PrepareRound` on `WorldReady` — see §3b.
Obsolete `IsSetUp` shims remain on Area/Electricity for call sites not yet migrated.

The specific client/host divergence this doc originally called out — flood-fill and `TileChunk._areaIds`
being server-only and never replicated, plus non-`BufferLast` lighting RPCs, so a client's `LightPower`
couldn't resolve area or lighting state — was the bug class `cursor/client-light-fixture-sync` was
chasing. **It shipped 2026-07-23** (PR #36); see §3a for what landed and why it validates this doc's
client-parity contract (orthogonal to the readiness graph).

**Still open:** Phase 3h — empty Boot/Game of per-system GameObjects (hub dual-runs with scene systems
today). See §4–§5.

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

**Status: shipped, 2026-07-23.** Behavioral contract met via PR #36 (Empty offline + retry guards);
named FSM + Empty codegen landed on the same-day implementation pass (`cursor/session-world-lifecycle`).

```
Cold → Connecting → Online → Disconnecting → WaitingForServer → Connecting → …
```

Owned by `ClientConnectionRecovery` (`SessionState` enum). Rules:

- **Cold start** may load Boot/Intro **once**. `SkipIntro`/`IntroUIHelper` auto-join fires **only** when
  `SessionState.Cold`, never on reconnect.
- **After any join attempt, offline is never Boot.** CCR arms `Scenes.EmptyPath` after first Online;
  `AutomationSubSystem` redirect never restores Boot offline.
- **Reconnect** via manual Retry (CCR OnGUI when Boot is gone; `ServerConnectionView` while Intro
  remains). **Not done:** exponential backoff. Never via Intro/`SkipIntro`.
- **Online** loads Game; **Disconnecting** → `WaitingForServer` with `SubSystems.SetSuppressMissingErrors`
  so locator misses stay silent.
- Prefer branching on `SessionState` / `ClientConnectionRecovery.Instance` over inferring session from
  subsystem presence.

Reconnect-to-body unchanged: `PlayerSubSystem.ProcessAuthorizePlayer` →
`EntitySubSystem.TryReclaimEntity` ([design/networking.md](../design/networking.md) §3).

**Key files:** `Networking/SessionState.cs`, `Networking/ClientConnectionRecovery.cs`,
`Networking/NetworkSessionSubSystem.cs`, `Networking/ServerConnectionView.cs`,
`Systems/Intro/IntroUIHelper.cs`, `Systems/Testing/AutomationSubSystem.cs`,
`Data/Generated/Scenes.cs` (`Empty` / `EmptyPath` / `BootPath`), FishNet `DefaultScene` in `Boot.unity`.

## 3. Phase 2 — World readiness graph

Separate **registered** from **ready**. Two pieces: **client-parity** (§3a, PR #36) and the
**general world-readiness graph** (§3b, shipped on the implementation branch).

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

### 3b. General world-readiness graph — shipped

**Status: shipped, 2026-07-23** (`cursor/session-world-lifecycle`). One contract replaces the old idioms:

- `IWorldReady` / `IsReady` / `WhenReady` on world subsystems + UniTask `WaitUntilAsync` on
  `WorldReadinessSubSystem`, phases:

  ```
  TileMapLoaded → AreasFlooded → ElectricityReady / AtmosReady / DisposalReady → WorldReady
  Persistence OnBeforeRestore → ResetEpoch; OnAfterRestore → TileMapLoaded (+ domain re-notify)
  ```

- Tile gate is end-of-restore / post-flood — **not** `OnMapCreated`. Atmos/Disposal await
  `TileMapLoaded` then notify their gates (no `CurrentMap != null` poll).
- Area notifies `AreasFlooded` after deferred flood; Electricity awaits AreasFlooded then notifies.
  Area/Electricity keep obsolete `IsSetUp` → `IsReady` shims.
- `RoundSubSystem.PrepareRound` awaits `WorldReadyPhase.WorldReady` (no fixed 500 ms delay).
- **Smell (harmless):** station restore can double-`ResetEpoch` — `OnBeforeRestore` handler **and**
  direct `NotifyStationTemplateRestoreBeginning()` from `PersistenceSubSystem` (logs epoch 1 then 2).
  Prefer one path when cleaning up.

**Key files:** `Core/WorldReadiness/{IWorldReady,WorldReadyPhase}.cs`,
`Systems/WorldReadiness/WorldReadinessSubSystem.cs`; Area / Electricity / Atmos / Disposal SubSystems;
`Systems/Persistence/PersistenceSubSystem.cs`; `Systems/Rounds/RoundSubSystem.cs`.

## 4. Phase 3 — Thin scenes + code bootstrap + `NetworkSystemsHub`

Realizes [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) follow-on **(a)**;
pays down [TECH_DEBT.md](TECH_DEBT.md) §1.7 / §1.12.

**Status: scaffolding shipped; Boot/Game empty still open.**

Shipped:

- `SystemsBootstrap` — DDOL WorldReadiness, ScreenEffects, Automation, Vision (no longer
  `RuntimeInitializeOnLoadMethod` / Game-placed Vision).
- `NetworkSystemsHub` Resources prefab; server spawns on Online (dual-runs with scene SubSystems).
- `SubSystems.Get` FindObject fallback skipped while quitting **or** `WaitingForServer`.
- Editor menus under `SS3D/Bootstrap/*` for migration.

Still open (Phase 3h):

- Empty `Boot.unity` / `Game.unity` of per-system GameObjects; migrate remaining scene systems onto
  bootstrap/hub so adding a system needs no scene YAML.
- Full reverse-order unregister on Online unload; Comms/`Human.prefab` wiring still manual.

**Key files:** `Systems/Bootstrap/SystemsBootstrap.cs`, `Networking/NetworkSystemsHub.cs`,
`Assets/Resources/NetworkSystemsHub.prefab`, `Core/Subsystems.cs`,
`Assets/Content/Scenes/{Boot,Game}.unity`.

## 5. Sequencing & acceptance criteria

1. **Phase 1** — session FSM + never re-enter Boot after first join. **Shipped** (PR #36 + FSM/Empty codegen).
2. **Phase 2** — world-ready + client parity. **§3a shipped** (PR #36); **§3b shipped** (readiness graph + PrepareRound gate).
3. **Phase 3** — code bootstrap + hub. **Scaffolding shipped**; **Boot/Game scene empty still open** (effort remains `in-progress`).

Per-phase acceptance:

- **Phase 1 — met.** Disconnect does not reload Boot after first Online; manual Retry reconnects;
  named `SessionState`; Intro auto-join Cold-only; no "already starting/started" storm. No backoff yet.
- **Phase 2, §3a — met.** Late-join pure client lights fixtures via SyncVar + floor-cache area resolve.
- **Phase 2, §3b — met.** Domains notify gates; `PrepareRound` awaits `WorldReady`; restore resets epoch
  and re-fires TileMapLoaded. Double-epoch reset smell documented, not blocking.
- **Phase 3 — partial.** Bootstrap + hub spawn + WaitingForServer-silent Get exist; most SubSystems still
  scene-placed (hub dual-runs). Full acceptance = no scene-placed gameplay SubSystems and no competing
  self-bootstrap styles.

Verification: Editor Play Mode host cold-start (Cold→Online→World ready→PrepareRound→Ongoing);
`Testing/multiplayer/run_smoketest.sh` reconnect scenarios; EditMode via
`.github/workflows/editmodetestrunner.yml`. Run `update-system-docs` after further Phase 3 migration.

## 6. Related docs

- Composition target: [2026-07_agent-first-composition.md](2026-07_agent-first-composition.md) (follow-on (a))
- Debt register: [TECH_DEBT.md](TECH_DEBT.md) §1.7 (bootstrap), §1.12 (`Get` FindObject), §1.16 (lifecycle)
- Design (read-only): [design/networking.md](../design/networking.md) §2–§5 (disconnect/reconnect/capacity)
- System maps: [networking-session](systems/networking-session.md), [scene-management](systems/scene-management.md),
  [application](systems/application.md), [core-subsystems](systems/core-subsystems.md),
  [player-control](systems/player-control.md), [tile](systems/tile.md), [area](systems/area.md),
  [electricity](systems/electricity.md), [atmospherics](systems/atmospherics.md), [disposal](systems/disposal.md),
  [persistence](systems/persistence.md), [rounds-lobby](systems/rounds-lobby.md)
