> Parent map: [networking-session.md](networking-session.md)
> Code paths: Assets/Scripts/SS3D/Systems/Testing/, Testing/multiplayer/
> Status: partial
> Verified: e87d38958 — 2026-11-01

# Multiplayer testing

## Overview

The smoke harness launches a real Linux dedicated server and separate headless clients, drives
`AutomationSubSystem` scripts, and checks each process's logs. The load runner adds configurable
client count, soak duration and server metrics. Graphical UI/session pilots have a separate
[runbook](../../../Testing/ui/README.md).

## Start here

- `Tools/build_client_and_server.sh` — Unity batchmode Linux builds; installed Unity required, project Editor closed. Outputs `Builds/Game/SS3D.x86_64` and `Builds/GameServer/SS3D.x86_64`.
- `Testing/multiplayer/run_smoketest.sh` — `<scenario> [client-count]`; server script is `<scenario>.txt`, client scripts prefer `<scenario>-client-N.txt`, then `<scenario>-client.txt`
- `Testing/multiplayer/run_loadtest.sh` — `--clients N --duration SECONDS --stagger SECONDS`; defaults 8, 300 and 0
- `Testing/multiplayer/lib/process.sh` — port allocation, build staging and process lifecycle
- `Testing/multiplayer/lib/logwait.sh` — signal waits and log failure gates
- `Testing/multiplayer/tools/triage_run.sh` — summarize a run by id/path or `latest`
- `Assets/Scripts/SS3D/Systems/Testing/AutomationSubSystem.cs` — code-bootstrapped DSL runner, enabled by `-testscript=<path>`
- `Assets/Scripts/SS3D/Systems/Testing/AutomationScript.cs` — instruction parsing
- `Assets/Scripts/SS3D/Systems/Testing/TestSignal.cs` — structured readiness/completion signals
- `.github/workflows/multiplayer-smoke-test.yml` — manual, labeled-PR and nightly smoke

## Extension points

- Add server/client scenario text under `Testing/multiplayer/scenarios/`. Use `wait_connected`, `ready`, `start_round`, `wait_round`, `embark`, `assert_embarked`, `reconnect`, `console`, `wait_timeout`, `wait_seconds`, `assert_reset_health` (server: N lines in `reset-health.jsonl`) and `disconnect`. A scenario's optional `scenarios/<name>.conf` (sourced) sets `SERVER_EXTRA_ARGS` and `CLIENT_TIMEOUT`; `embark` requests spawning, `assert_embarked` confirms control of a body.
- Expose deterministic assertions through `console` commands or DSL instructions. Emit distinctive signals and throw on failed assertions so the runner emits `ScriptFailed`.
- Graphical clients also accept `ui_wait`, `ui_text`, `ui_submit`, `os_click`, `os_type`, `os_key` and `screenshot`. `ui_submit` is synthetic navigation; the remote pilot services `os_*` through the OS-input bridge.
- Override builds with `SS3D_SERVER_BUILD_DIR`, `SS3D_CLIENT_BUILD_DIR`, `SS3D_SERVER_BIN_NAME` and `SS3D_CLIENT_BIN_NAME`. Both runners accept `SS3D_TILEMAP_FIXTURES` and `SS3D_STATION_FIXTURES` for map data.
- Use structured `Test signal` records for waits. Keep compact JSON file logging enabled; Unity exceptions also require checking `unity.log`.

## Pitfalls

- **Round start silently denied:** staged clients need Administrator permissions. Clear stale staged ServerMeta permissions before seeding `Config/permissions.txt`; the persistence envelope otherwise overrides it.
- **No station / zero atmos cells:** player build directories can lack CWD map data. Stage valid fixtures; content availability and validation requirements still apply to scripted starts.
- **Headless client crashes before game code:** Linux clients without DISPLAY need `SDL_VIDEODRIVER=dummy`; `process.sh` supplies it. Dedicated servers do not require it.
- **Headless UI instructions time out:** lobby documents skip bootstrap under batchmode/Null GFX. Use a graphical client for UI assertions and screenshots.
- **A completed windowed client never exits:** key success on `ScriptComplete`, then clean up leftovers; process termination alone is not the completion signal.
- **Noise classification hides a regression:** `known_unity_bad.patterns` is a failure gate. `known_unity_noise.patterns` is triage-only; never move pure-client SyncVar-write warnings into the noise list.
- **Disk usage jumps:** staging hardlinks immutable build files and copies writable Config/Data/Logs. Different filesystems require a full-copy fallback.

## Tests

Run the smoke scenarios after building:

```bash
./Testing/multiplayer/run_smoketest.sh basic-round
./Testing/multiplayer/run_smoketest.sh late-join 2
./Testing/multiplayer/run_smoketest.sh reconnect
./Testing/multiplayer/run_smoketest.sh atmos-client-sync
./Testing/multiplayer/run_smoketest.sh sandbox-reset
./Testing/multiplayer/tools/triage_run.sh latest
```

`basic-round` covers connect/ready/start/embark and a console round trip; `late-join` covers
embark after Ongoing; `reconnect` checks reclaiming a body; `atmos-client-sync` forces heat then
asserts a client snapshot; `sandbox-reset` runs a `-sandbox` server with one-minute rounds and
checks that a client that never readies joins two automatic rounds and the reset writes `reset-health.jsonl`. The CI workflow runs this set on dispatch, PR label `test:multiplayer`
and a nightly schedule. GitHub disabled the workflow for inactivity after the 2026-10-19
run (`gh workflow list --all` shows `disabled_inactivity`), so none of these triggers fire until it
is re-enabled. Configured checks do not establish a current passing run.

With the Editor open, `Testing/ui/validate.sh --full` runs compile, EditMode, PlayMode, visual and
lifecycle pilots, stale builds, remote pilot and smoke. Reports live under `artifacts/ui/`;
smoke logs are preserved under `Testing/multiplayer/.runs/`.

Load example: `./Testing/multiplayer/run_loadtest.sh --clients 8 --duration 300 --stagger 2`.
Logs/`metrics.csv` live under `.runs/load-*`. Every 10 s the runner samples server RSS/CPU/threads
and host-wide loopback bytes/rates; run on an otherwise idle host. Embark counts are observed
signals, not continuous liveness. No performance thresholds or scheduled 24/7 soak are enforced.

Coverage gaps: rendered selection/drop/pickup, dedicated-client pocket/container round trips,
the complete Nuke Ops loop, and Windows clients. The headless runner cannot prove visual behavior.

## Depends on / Used by

- **Depends on:** [networking-session](networking-session.md), [permissions](permissions.md), Linux builds, `jq`
- **Used by:** release/smoke CI, [UI validation](../../../Testing/ui/README.md), multiplayer triage skills

## Related docs

- [CI build/release](../2026-07_ci-develop-release-pipeline.md), [self-hosted CI work](../2026-07_multiplayer-testing-self-hosted-ci.md)
- [Verification debt](../TECH_DEBT.md#110-play-mode--multiplayer-verification-is-optional-in-practice)
