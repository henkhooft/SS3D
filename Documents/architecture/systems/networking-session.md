> Code paths: Assets/Scripts/SS3D/Networking/, Assets/Scripts/SS3D/Editor/ServerBuildScript.cs, Assets/Scripts/SS3D/Editor/ClientBuildScript.cs, Assets/Scripts/SS3D/Systems/Testing/, Testing/multiplayer/
> Entry points: NetworkSessionSubSystem, ClientConnectionRecovery, NetworkSystemsHub, SS3D.Systems.Testing.AutomationSubSystem
> Status: partial
> Verified: 290aa4e1c — 2026-07-28 (chunked station load; CCR Linux fonts)

# Networking (session)

## Overview

FishNet session management — host/join, network type and port settings. Distinct from tile AOI helpers under `Systems/Networking/`. Includes a genuine headless dedicated-server build (`UNITY_SERVER` subtarget), not just a client build launched with `-serveronly` — see [2026-07_headless-dedicated-server](../2026-07_headless-dedicated-server.md). A real multi-process test harness now exercises this end to end — see [2026-07_multiplayer-test-harness](../2026-07_multiplayer-test-harness.md). Manual CI prerelease + nightly `develop-nightly` (EditMode/smoke opt-in on dispatch): [2026-07_ci-develop-release-pipeline](../2026-07_ci-develop-release-pipeline.md). Self-hosted TomNAS runner (smoke + EditMode/release with GitHub fallback): [2026-07_multiplayer-testing-self-hosted-ci](../2026-07_multiplayer-testing-self-hosted-ci.md).

## Start here

- `Assets/Scripts/SS3D/Networking/SessionState.cs` — `Cold → Connecting → Online → Disconnecting → WaitingForServer`
- `Assets/Scripts/SS3D/Networking/ClientConnectionRecovery.cs` — DDOL session FSM owner; arms `Scenes.EmptyPath` offline after first Online; OnGUI Retry/Quit; suppresses `SubSystems.Get` Errors while WaitingForServer; spawns `NetworkSystemsHub` on server Online
- `Assets/Scripts/SS3D/Networking/NetworkSessionSubSystem.cs` — session host/join; gates on `ClientConnectionRecovery.CanStart`; UNITY_SERVER auto-starts on `ApplicationInitializing`
- `Assets/Scripts/SS3D/Networking/NetworkSystemsHub.cs` + `Assets/Resources/NetworkSystemsHub.prefab` — Online hub NetworkObject (all Game NetworkSubSystems)
- `Assets/Scripts/SS3D/Networking/ServerConnectionView.cs` — Intro connection progress/fail UI; Retry calls `StartNetworkSession` again
- `Assets/Scripts/SS3D/Systems/Bootstrap/SystemsBootstrap.cs` — DDOL process-wide (incl. NetworkSession via type name)
- `Assets/Scripts/SS3D/Systems/Testing/AutomationSubSystem.cs` — harness script runner (bootstrapped via SystemsBootstrap); Empty offline redirect never restores Boot
- `Assets/Scripts/SS3D/Editor/Bootstrap/SessionWorldLifecycleEditorMenus.cs` — **SS3D → Bootstrap → Rebuild NetworkSystemsHub Prefab** (tier A); Phase 3h strip helpers remain as `-executeMethod` statics only
- `Testing/multiplayer/run_smoketest.sh` — multiplayer harness

## Extension points

- Boot.unity's `ServerManager._startOnHeadless` must stay `0`.
- Boot.unity `DefaultScene._offlineScene` stays Boot for cold start; CCR arms Empty after first Online.
- Prefer `SessionState` / `ClientConnectionRecovery.Instance` over inferring session status from subsystem presence.
- New networked SubSystem: add to hub rebuild (**SS3D → Bootstrap → Rebuild NetworkSystemsHub Prefab**) — never hand-edit Game.unity.

## Pitfalls

- **Host `-host` without a loopback address.** Built apps call `NetworkSettings.ResetOnBuiltApplication()` which clears `ServerAddress`. Older `Start_SS3D_Host.bat` omitted `-ip=`, so Host's local client called `StartConnection("", port)`. LiteNetLib resolves empty via DNS (IPv6-first) → link-local `fe80::…` instead of `127.0.0.1`. Symptom (esp. Wine): world loads / WorldReady OK, then `Client 0 fe80::… disconnected` spam ≈ every 500 ms, session → WaitingForServer, empty OnGUI Retry/Quit, lobby stuck `Stopped - 0` (no stable Player auth). Fix: Host defaults empty address to `127.0.0.1`; `-ip=` only sets address (no longer forces Client); Host.bat includes `-ip=127.0.0.1`. **Existing zips:** edit the bat to `-ip=127.0.0.1 -host -port=1151 …` (`-ip` before `-host` on pre-fix builds so NetworkType stays Host).
- **CCR OnGUI + OS fonts.** Recovery dialog uses IMGUI; player default font fails under Wine (`Unable to load font face for [LegacyRuntime]` × per frame → blank buttons). Prefer **installed** OS fonts via `Font.GetOSInstalledFontNames` (Windows: Segoe UI; Linux: DejaVu/Liberation/Noto — never lead with Segoe UI on Linux or CreateDynamicFontFromOSFont still binds it and spams blank buttons). Hit 2026-07-28 (Fedora MetaStation import).
- **DMM MetaStation import can drop Host session:** a synchronous full-map `PlaceTileObject` loop blocks the main thread long enough that FishNet/LiteNetLib heartbeats fail → CCR Retry/Quit. Import window applies via `MapImportApplier.ApplyRoutine` (yield every 64 placements). Hit 2026-07-28.
- **Station template boot load freezes the same way:** `LoadMostRecentStationTemplate` used to place sync; use `LoadStationTemplateAsync` / `TileMap.LoadRoutine` (Play start + Map Editor Load). Hit 2026-07-28.
- **Disconnect must not reload Boot after first Online.** CCR arms Empty; Automation must not restore Boot offline after reconnect.
- **Intro auto-join is Cold-only** (`IntroUIHelper` checks `SessionState.Cold`).
- **`SubSystems.Get` during Disconnecting / WaitingForServer** is silent (`SetSuppressMissingErrors`) — prefer `TryGet`. Suppress starts on Disconnecting so hub/device `OnDestroy` during `StopConnection` does not Error before WaitingForServer.
- **OnGUI recovery when Empty offline:** NetworkSession is DDOL — CCR shows OnGUI when Intro/Boot/Launcher are not loaded (not when NetworkSession is missing).
- See also prior harness / headless pitfalls below (unchanged).
- **After `ScriptComplete`, hard-exit — do not `Application.Quit`.** Quit still unloads scenes and re-enters `ApplicationInitializing`, so NetworkSession re-joins and (without a guard) automation re-runs → harness Error/Fatal + RoleSubSystem duplicate-key. `AutomationSubSystem` runs the script once and `Environment.Exit(0)` after emitting the final signal.
- **`TileResourceLoader` / `Item.GenerateIcon` preview cameras break `-nographics` clients.** `IconPreviewGenerator` → `RuntimePreviewGenerator` recreates URP on NullGfxDevice → GraphicsBuffer/Blitter spam that fails the harness exception check. Skip icon generation when `Application.isBatchMode` or `GraphicsDeviceType.Null` (dedicated server already skipped via `UNITY_SERVER`).
- **Linux client without DISPLAY SIGSEGVs unless `SDL_VIDEODRIVER=dummy`.** Unity 6 picks window backend `(null)` and dies in `PlayerMain`. Dedicated server builds are unaffected. Smoke harness sets the default in `Testing/multiplayer/lib/process.sh`.
- **Destroyed `BasicElectricDevice` throws on `TileObject` during server teardown.** Accessing `.gameObject` on a destroyed component NREs inside electricity FixedUpdate after `StopConnection`; `TileObject` now returns null when `this` is Unity-destroyed so area/APC lookups bail cleanly.
- **FishNet SyncVar writes on pure clients are LogWarnings, not exceptions.** Smoke used to pass while `unity.log` filled with `Cannot complete operation as server when server is not active` (e.g. injury SyncVars from `HumanoidBodyStateBridge`). Harness now hard-fails on `tools/known_unity_bad.patterns`; fix with `IsServer` guards, never by allowlisting as noise. See [entities.md](entities.md). Release player stacks often strip to `NetworkActor:Start` — `SyncBase.LogServerNotActiveWarning` embeds `Behaviour=` / `Object=` / `ObjectId=`, and `AutomationSubSystem` re-logs that under `[SS3D SyncVarGuard]` for triage. Named hit: [substances](substances.md) `SubstanceContainer` / `OxygenTank` seeding from Unity `Start`.
- **Disconnect / failed join must not reload Boot as offline after a successful session.** FishNet `DefaultScene` offline is Boot for cold start so Intro works; after the first Started connection, `ClientConnectionRecovery` arms offline=`Empty.unity`. Otherwise `StopConnection` → `LoadScene(Boot)` while NetworkManager is DDOL re-ran Intro/`SkipIntro`/`StartNetworkSession` hundreds of times (`Failed to start the client connection… already starting/started`) plus DOTween missing-target noise. `StartNetworkSession` also skips when the client is still Starting/Started/Stopping. Mid-game drop uses `ClientConnectionRecovery` OnGUI (Boot is gone); Intro fail uses `ServerConnectionView` Retry → `StartNetworkSession`. Harness still calls `RedirectOfflineSceneToEmpty` before disconnect as a belt-and-suspenders.
- **`-testscript=` clients: `IntroUIHelper` must not auto-`StartNetworkSession`.** SkipIntro would otherwise race Automation's owned join/reconnect. Automation starts the first client session from `HandleApplicationInitializing`.
- **Client teardown must not call `ServerManager.Despawn`.** `PlacedItemObject.DestroySelf` on pure clients after `StopConnection` used to spam `Cannot despawn object because server nor client are active`; dispose the GameObject locally instead.

## Depends on / Used by

- **Used by:** All `NetworkSubSystem` domains, [tile](tile.md) AOI

## Related docs

- [INDEX.md](../INDEX.md)
- [2026-07_headless-dedicated-server](../2026-07_headless-dedicated-server.md) — headless
  server build, runtime guards, known issues, testing-harness gap
- [2026-07_multiplayer-test-harness](../2026-07_multiplayer-test-harness.md) — the harness that closes that gap
- [2026-07_ci-develop-release-pipeline](../2026-07_ci-develop-release-pipeline.md) — manual + nightly CI prerelease (EditMode/smoke opt-in on dispatch)
- [2026-07_multiplayer-testing-self-hosted-ci](../2026-07_multiplayer-testing-self-hosted-ci.md) — TomNAS self-hosted runner + warm Library stash + harness coverage growth
