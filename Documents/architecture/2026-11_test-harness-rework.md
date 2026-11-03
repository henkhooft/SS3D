> Implements: [2026-11_testing-philosophy.md](2026-11_testing-philosophy.md) (infrastructure; no design doc)
> Touches systems: networking-session, rounds-lobby, health, inventory, id-access, atmospherics, ingame-console
> Status: in-progress

**Progress:** Phase 1 done (2026-11-03); Phase 2 next.

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

- [ ] Test room fixture map under the existing fixtures (`SS3D_TILEMAP_FIXTURES` /
      `SS3D_STATION_FIXTURES` locations), validated by `MapValidationTests`.
- [ ] Direct-spawn path for a Play Mode host: load the fixture and give the host a body without lobby
      or ready. Check first whether `RoundSubSystem.ApplyServerSettingsForTesting` (sandbox profile)
      already covers most of it.
- [ ] `ScenarioFixture` base: one-time boot, fresh body per scenario, tracked spawn cleanup, optional
      room reset, `LogAssert` left at its failing default.
- [ ] Console helper that runs a command as the host and returns its output; thin helpers for "use
      held item on self" and reading health.
- [ ] **Medical** scenario: spawn a medkit in hand, `hurt`, apply a bandage, assert health improved.
- [ ] Measure: boot cost and per-scenario cost. If one boot doesn't make scenarios cheap
      (target: under ~10 s each after boot), stop and revisit D1 before Phase 3.

## Phase 3 — First scenario set; retire pilot partials

- [ ] **Access**: a door with an access requirement; denied without an ID, opens with a granted ID card.
- [ ] **Environment**: vent or overheat the room; atmos damage reaches critical. Replaces
      `LobbyLifecyclePilot.Ragdoll` and `.Reclaim`.
- [ ] **Death and respawn**: kill, ghost, respawn, new body is controllable. Replaces `.Respawn`.
- [ ] **Speech**: critical-state speech gates. Replaces `.Speech`.
- [ ] Remove the replaced partials and their `validate.sh` focused steps (`ragdoll-space`,
      `respawn-cleanup`, `critical-speech`, `critical-reclaim`); point those step names at the
      scenario categories in the runbook.
- [ ] Each touched system map names its scenario category under **Tests** (D10).

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
