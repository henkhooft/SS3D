> Implements: [2026-11_testing-philosophy.md](2026-11_testing-philosophy.md) (infrastructure; no design doc)
> Touches systems: networking-session, rounds-lobby, health, inventory, id-access, atmospherics, ingame-console
> Status: in-progress

**Progress:** Phases 1–3 done (2026-11-03); Phase 4 next.

# Test harness rework (Nov 2026)

Bring the harness in line with the [testing philosophy](2026-11_testing-philosophy.md): a fast
targeted loop for agents, cross-system scenarios in one Play Mode host, a curated multiplayer set,
and expensive suites in the nightly on TomNAS.

## Why

- Agents ran `validate.sh --full` (~25 min warm) after every feature, because the docs and the
  script's defaults pointed them there. The docs are fixed; the script is Phase 1.
- PlayMode is 9 tests in ~6.5 min. Every test reloads `Boot` and waits for the lobby
  (`PlayModeTest.LoadAndSetInLobby`), and all of them set `LogAssert.ignoreFailingMessages = true`,
  so errors never fail them. Round start/stop is covered in five places.
- `LobbyLifecyclePilot` absorbed feature validation (Speech, Ragdoll, Reclaim, Overlay, Respawn
  partials) without anyone deciding those checks should be permanent.
- Nothing tests cross-system gameplay cheaply. Console commands and the `AutomationSubSystem` DSL
  exist, but every runtime check pays for a lobby and a round start.

## Decisions

| # | Decision |
|---|----------|
| D1 | Tier 2 scenarios are NUnit PlayMode tests. One boot per fixture into a test room; each `[UnityTest]` is one scenario. Selection by NUnit category, so MCP `run_tests` can run them targeted and batchmode `-runTests` can run them nightly. |
| D2 | The world is a tiny checked-in test room fixture (a few tiles with air, power, a door with access and a table). The host spawns straight into a body: no lobby, no ready. |
| D3 | Isolation: a fresh body per scenario, plus tracked cleanup of everything the scenario spawned. Room reset (atmos, doors) only for scenarios that change the room. Order between scenarios is irrelevant. |
| D4 | Scenarios run their steps through a console helper (`Console("spawnitem …")`) so they stay portable to the DSL. Any logged error or exception fails a scenario; intended ones are declared with `LogAssert.Expect`. |
| D5 | Multiplayer set: `late-join` (absorbs `basic-round`), `reconnect`, `observer-sync` (replaces `atmos-client-sync`; a second client checks atmos, held item, health and door state). `sandbox-reset` runs nightly only. The nightly keeps the real dedicated-server build. |
| D6 | Pilots: Speech, Ragdoll, Reclaim and Respawn become scenarios. Overlay stays a visual step. The lifecycle core, `LobbyUiPilot` and the remote client pilot run on demand and in a weekly graphical run on TomNAS. The sandbox pilot is split: rules in EditMode (`SandboxServerTests`), the reset loop in smoke, the bug-report dialog as an on-demand visual step. |
| D7 | Wall-clock budgets on TomNAS (N100): nightly editor suites 15 min, smoke 20 min, weekly graphical 20 min. Over budget triggers a cull before that tier gets anything new. |
| D8 | Each tier 2–4 check has a header: `// Guards: <contract or issue #> · Not cheaper because: <reason>`. |
| D9 | A failing nightly opens or updates one GitHub issue labelled `nightly-failure` that closes on the next green run. Agents check the last nightly at session start. |
| D10 | Each system map's **Tests** section names the validate step and scenario category for that domain. Automatic changed-path selection only if agents still pick wrong. |
| D11 | ParrelSync is removed. Unity Multiplayer Play Mode gets a time-boxed spike instead. |

## Phase 1 — Quick wins

- [x] `validate.sh`: default steps become `compile,editmode`. Add `scenarios:<category>`. Rename
      `--full` to `--nightly` (mirrors the CI nightly) and add `--weekly` (the graphical set). Update
      the usage line, the README step table and every doc that cites `--full`.
      Done: the default is `preflight,compile,editmode`. `--quick` is gone. `--nightly` runs editor
      tests, PlayMode, builds and smoke; `--weekly` runs the visual, lifecycle, sandbox, build and
      remote steps. Archived docs and dated incident notes keep `--full`.
- [x] `validate.py` output: one line per passed step; failure details and report paths only for
      failures; list screenshots only when a visual step ran.
- [x] PlayMode triage: delete `Issue0990_PropagatingPocketProblem_HostPerspective` and
      `HostGameActions`. Move the four `RoundLifecycle_*` races to EditMode against the round state
      machine where a pure seam exists. Keep any that can't move as nightly-only, with
      `ignoreFailingMessages` removed.
      Done: the orphaned `IssueReproduction` and `PlaymodeTestRepository` went too. The only pure
      seam was the start/stop gate, now `RoundRequestRules` with `RoundRequestRulesTests`. The
      races themselves (cancellation, despawn, restart, late embark during Ending, gamemode end on
      a dedicated server) stay in PlayMode, tagged `Nightly`. `PlayModePrerequisites` lost its
      compiled-build check, a duplicate of the EditMode one.
- [x] Remove ParrelSync: delete `Assets/Scripts/External/ParrelSync` and its uses in
      `SS3D.Data.asmdef`, `Paths.cs` and `NetworkSettings.cs`. FishNet's references sit behind the
      `PARRELSYNC` define and need no change.
      Done: `SS3D.Networking.asmdef` also referenced it by GUID. FishNet's asmdef GUID reference
      is left dangling (vendor file).
- [x] Add a `Guards:` header (D8) to every remaining PlayMode test, pilot and smoke scenario, or
      delete it if it can't justify its cost.

## Phase 2 — Scenario framework and the medical scenario

- [x] Test room fixture map under the existing fixtures (`SS3D_TILEMAP_FIXTURES` /
      `SS3D_STATION_FIXTURES` locations), validated by `MapValidationTests`.
      Done: the owner built it in the map editor. It lives at `Testing/fixtures/stations/testroom.json`,
      not under `Builds/Game/Data/StationTemplates`: the release workflow ships that folder and the
      lobby lists it. Main room 5×5 (Assistant and Security spawns, table, lights) and side room 3×5
      (Security area), joined by a `SecurityAirlock`. Each room has its own area and APC, fed by an
      SMES, plus an unconnected vent and scrubber, so neither room's air can reach the other.
      `TestRoomFixtureTests` (EditMode) checks Map Check for Extended, spawns on plenum, one Security
      door and the Security area.
- [x] Direct-spawn path for a Play Mode host: load the fixture and give the host a body without lobby
      or ready. Check first whether `RoundSubSystem.ApplyServerSettingsForTesting` (sandbox profile)
      already covers most of it.
      Done: `ScenarioFixture` copies the room in as `scenario-testroom`, applies Extended settings
      with that map through `ApplyServerSettingsForTesting`, readies the host, starts the round and
      skips warmup with the admin `ForceStart`. The sandbox profile itself wasn't needed.
- [x] `ScenarioFixture` base: one-time boot, fresh body per scenario, tracked spawn cleanup, optional
      room reset, `LogAssert` left at its failing default.
      Done: one boot per run, shared by every scenario fixture. Fresh bodies come from the editor-only
      `EntitySubSystem.DespawnBodyForTesting` / `SpawnBodyForTesting`, with both hands emptied of
      the job loadout. Items and entities a scenario spawned are despawned afterwards. Setting
      `ChangesRoom` (or calling `ReloadRoom()`) restarts the round. Added at the owner's request:
      every load asserts both rooms hold standard station air (`ScenarioAtmos`), guarding the old
      round start/stop atmos reset bug; a scenario that changes the air without declaring it fails;
      breathing CO₂ past 0.5% triggers a reload.
- [x] Console helper that runs a command as the host and returns its output; thin helpers for "use
      held item on self" and reading health.
      Done: `ScenarioConsole` awaits `CommandsController.AnswerShown`. New `spawninhand <item>`
      command. "Use held item" is the editor-only `InteractionController.RunInteractionForTesting`,
      which sends the click RPC so the server revalidates. Health is read from `HumanHealthController.Snapshot`.
- [x] **Medical** scenario: spawn a medkit in hand, `hurt`, apply a bandage, assert health improved.
      Done as `MedicalScenarios.BandageOnSelf_StopsBleeding` (chest and left leg): `MedicalPatch`
      in hand, `hurt` 30 brute, bandage with Help intent, bleeding stops. The bandage stops bleeding
      but heals no brute, so "health improved" is "bleeding stopped".
- [x] Measure: boot cost and per-scenario cost. If one boot doesn't make scenarios cheap
      (target: under ~10 s each after boot), stop and revisit D1 before Phase 3.
      Measured on the dev box (2026-11-03): boot 6.2 s, fresh body 0.1–0.2 s, scenario 0.3–0.4 s,
      room reload 4.5 s. All 11 PlayMode tests (scenarios, lifecycle, prerequisites) run in 96 s.
      D1 stands.

## Phase 3 — First scenario set; retire pilot partials

- [x] **Access**: a door with an access requirement; denied without an ID, opens with a granted ID card.
      Done as `AccessScenarios` (`Access`): the job ID is deleted and the door refuses by proximity
      and by click; a spawned `IDCard` bound to a record with no access is refused too; after
      `accessgrant Security` the door opens. The scenario revokes, steps back and waits for the door
      to close, so it doesn't need a room reload.
- [x] **Environment**: vent or overheat the room; atmos damage reaches critical. Replaces
      `LobbyLifecyclePilot.Ragdoll` and `.Reclaim`.
      Done as `EnvironmentScenarios` (`Environment`). Venting and heat are too slow (vacuum takes
      ~60 s of oxy debt; burns don't make a body critical), so the scenario adds 10,000 mol of plasma
      at the body's tile: critical from toxin after ~17 s, collapsed with ragdoll gravity on the deck.
      That is the floor: intake is capped at 0.08/tick against 0.03/tick liver clearance. The ragdoll
      space checks and the reclaim checks are their own scenarios, made critical by a direct toxin
      tick (`ScenarioFixture.MakeCritical`), since no console command does it.
      Follow-up: the plasma wait is now the slowest scenario. A faster way to reach the same effect
      through the real atmos → health path would cut it; worth finding before Phase 4's budgets bite.
- [x] **Death and respawn**: kill, ghost, respawn, new body is controllable. Replaces `.Respawn`.
      Done as `RespawnScenarios` (`Respawn`), two cycles. "Controllable" is checked as: the host
      owns the new body and `spawninhand` fills its hand.
- [x] **Speech**: critical-state speech gates. Replaces `.Speech`.
      Done as `SpeechScenarios` (`Speech`).
- [x] Remove the replaced partials and their `validate.sh` focused steps (`ragdoll-space`,
      `respawn-cleanup`, `critical-speech`, `critical-reclaim`); point those step names at the
      scenario categories in the runbook.
      Done: `validate.sh` refuses the old names and says which category replaced each one.
- [x] Each touched system map names its scenario category under **Tests** (D10).
      Done for entities, chat-audio-screens, health and id-access.

Measured on the dev box (2026-11-03): all nine scenarios in one run take 59–72 s, boot included.
Access ~10 s (most of it waiting to prove the door stays shut), plasma ~20 s, the rest 0.4–7 s.

The fixture now copes with death: cleanup keeps the host's current entity (ghost or respawned body),
despawns a corpse left behind, and despawns loadout items that sit outside a container. The first
full run surfaced a real bug: `SmesBattery` leaked an `OnTick` handler whenever a client left
and re-entered its observer range (see [electricity](systems/electricity.md) Pitfalls).

## Phase 4 — Nightly

- [ ] Batchmode PlayMode run (scenarios plus any nightly-only PlayMode tests) on TomNAS, added to
      the nightly next to `multiplayer-smoke-test.yml`.
- [ ] Smoke set per D5: fold `basic-round` into `late-join`, write `observer-sync`, retire
      `atmos-client-sync`, move `sandbox-reset` to nightly-only. The light-sync scenario planned in
      [2026-07_multiplayer-testing-self-hosted-ci](2026-07_multiplayer-testing-self-hosted-ci.md)
      Phase 2 becomes a check inside `observer-sync`; its Phase 1 server-kill scenario stays a
      separate contract, within the smoke budget.
- [ ] Budgets (D7): each nightly job prints its elapsed time against its budget.
- [ ] `nightly-failure` issue (D9), and a session-start line in `AGENTS.md` telling agents to check
      the last nightly.
- [ ] Weekly graphical run on TomNAS (forced HDMI output): `LobbyUiPilot`, lifecycle core, remote
      client pilot.

## Phase 5 — Multiplayer Play Mode spike (half a day)

- [ ] Editor host plus one or two virtual players, FishNet, running `late-join` and `reconnect` to the
      same pass/fail as the built smoke run.
- [ ] On success: agents use it for local multiplayer checks; built smoke stays the nightly gate
      (it exercises `UNITY_SERVER` code paths). On failure: record why in
      [networking-session-testing](systems/networking-session-testing.md) Pitfalls.

## Pitfalls (anticipated)

- **Teleporting the host body far away changes what its client observes.** Objects out of range run
  `OnStopClient`, and `OnStartClient` again on return. Handlers that subscribe on start and only
  unsubscribe on destroy leak (`SmesBattery`, fixed). The errors show up in the *next* scenario's
  set-up, after a room reload destroys the object, so suspect the previous scenario.

- **Despawning a host's body before its client spawned it.** On a Play Mode host, server lookups see a
  new body at once, but the host's client spawns it a few ticks later. Despawning it in between
  leaves the client processing a spawn with nested objects for an object already gone (FishNet
  `could not be found for a nested spawn` errors). Wait for `NetworkObject.ClientInitialized`.
  Hit 2026-11-03 in `ScenarioFixture`.

- **Scenarios pass because of leftovers.** A shared boot leaks state between scenarios. D3 is the
  guard; a scenario that only passes when run after another one is a bug in its cleanup.
- **Server-build subtarget leaks into the editor.** Batchmode nightly runs after a server build can
  inherit `UNITY_SERVER` assemblies (see the `IsHeadless` pitfall in
  [networking-session](systems/networking-session.md)). Run PlayMode before the server build, or in
  a fresh editor process.
- **Budget creep through "just one more" checks.** D7 exists for this; cull before adding.

## Related docs

- [2026-11_testing-philosophy.md](2026-11_testing-philosophy.md): the policy this implements.
- [Testing/ui/README.md](../../Testing/ui/README.md): `validate.sh` runbook.
- [systems/networking-session-testing.md](systems/networking-session-testing.md): smoke harness.
- [2026-07_multiplayer-testing-self-hosted-ci.md](2026-07_multiplayer-testing-self-hosted-ci.md): TomNAS runner and planned smoke scenarios.
