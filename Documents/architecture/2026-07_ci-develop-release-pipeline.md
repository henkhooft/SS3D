> Implements: Documents/architecture/2026-07_multiplayer-test-harness.md (CI), Documents/architecture/2026-07_headless-dedicated-server.md
> Touches systems: networking-session
> Status: shipped

# CI develop-release pipeline (Jul 2026)

## Goal

Give this fork a path from a known commit to a downloadable **Windows** player zip
(self-host via `Builds/*.bat`), with optional secondary Linux client/server artifacts.
Default **manual** dispatch is **Windows → prerelease** (~one player build). Linux (~30–45 min),
EditMode, and multiplayer smoke are **opt-in** — day-to-day cuts should not pay for
gates/`StandaloneLinux64` that `editmodetestrunner.yml` / `multiplayer-smoke-test.yml`
already cover separately. A **nightly schedule** (05:00 UTC) always builds Windows + Linux
client + Linux dedicated server and publishes a floating `develop-nightly` prerelease.
Not triggered on every develop merge (license cost + release spam at high PR volume).

## Shipped

### Workflow: `.github/workflows/develop-release.yml`

**Triggers:** `workflow_dispatch` + nightly `cron: "0 5 * * *"` (after smoke at 04:00 so
TomNAS `Library` is warm). `concurrency: develop-release` / `cancel-in-progress: false`.

**Nightly (schedule):** forces `build_linux=true`, `skip_release=false`, EditMode/smoke off,
`tag_suffix=nightly` → tag `develop-nightly` (overwritten each night).

**Inputs (manual; defaults favour a fast Windows cut):**

| Input | Default | Effect |
|-------|---------|--------|
| `build_linux` | `false` | When true, also build Linux dedicated server + client and attach Linux zips. |
| `run_editmode` | `false` | When true, EditMode job runs first and must pass before player builds. |
| `run_smoke` | `false` | When true, after Linux builds run smoke scenarios (needs `build_linux`). |
| `skip_release` | `false` | When true, skip Windows build and GitHub prerelease (Linux-only if `build_linux`). |
| `tag_suffix` | _(empty)_ | Override tag suffix; full tag is `develop-<suffix>` (else short SHA). |
| `runner` | `auto` | `auto` / `tomnas` / `github` — TomNAS when online, else `ubuntu-latest`. |

**Jobs:**

1. **Prepare** — resolve schedule vs dispatch flags; probe whether `TomNAS-unity` is online
   and set `runs_on` / `self_hosted` for build jobs.
2. **EditMode** (opt-in) — same `game-ci/unity-test-runner@v4` / `6000.3.16f1` pattern as
   `editmodetestrunner.yml`, including secret preflight and `environment: unity_tests`.
   Skipped when `run_editmode` is false (always skipped on nightly).
3. **Linux build** (nightly, or opt-in via `build_linux`) — **one** `unity-builder` step with
   `ClientAndServerBuildScript.BuildBothForCi` (avoids TomNAS PackageCache race from two
   sequential builder sessions). `versioning: None`. Unity 6 Linux binaries are
   `SS3D-Server` / `SS3D-Client` (no `.x86_64` suffix). On TomNAS: restore/save shared
   `/home/cu6e/.cache/ss3d/Library` stash + `runAsHostUser: true`.
4. **Smoke** (opt-in step on the Linux job) — harness scenarios when `run_smoke`; logs uploaded.
5. **Windows client** — default manual path and always on nightly; `StandaloneWindows64`
   (`buildName: SS3D`). Same TomNAS Library stash / `runAsHostUser` when self-hosted. Not
   smoke-tested on Windows. Skipped when `skip_release` is set.
6. **Prerelease** — primary zip `SS3D-Windows-<tag>.zip` with layout:
   `Start_SS3D_*.bat` + `README.txt` beside `Game/SS3D.exe`, plus CWD fixtures
   `Game/Config/` (`permissions.txt`, `network.json`) and `Game/Data/Tilemaps/`
   copied from git-tracked [`Builds/Game/`](../../Builds/Game/) (Unity does not pack these;
   `Paths.cs` reads them from process CWD). Never ships `Data/ServerMeta/` (runtime).
   Linux client/server zips get the same seed under each `StandaloneLinux64/` tree when
   Linux was built. Tag `develop-nightly` (schedule) or `develop-<shortsha>` /
   `tag_suffix`. `prerelease: true`.

### Related workflow changes

- `multiplayer-smoke-test.yml` — label-gated PR + `workflow_dispatch` + nightly 04:00 UTC;
  TomNAS + `BuildBothForCi`. Prefer this (or `build_linux` + `run_smoke` on develop-release)
  when you need a smoke gate.
- `editmodetestrunner.yml` — cheap PR/`develop` EditMode; prefers TomNAS when online,
  else `ubuntu-latest`. Prefer this (or `run_editmode: true` on develop-release) when you
  need an EditMode gate on a cut.
- `main.yml` (Automated Build) — **removed**; Windows prereleases go through
  `develop-release.yml`. Upstream milestone/project YAML deleted (`23b75ad36`); ghost
  registrations for `AddToRoadmap.yml` and old `testrunner.yml` **disabled** in the repo
  Actions settings (`gh workflow disable`) so they no longer fail on pushes.

## Pitfalls recorded for operators

- **Player zips must seed `Config/` + `Data/Tilemaps/` next to the binary.** Symptom on
  `develop-f1c9476` Windows: starts, empty `Game/Data/`, no `Config/`, host log
  `No station templates found to load` — Addressables were fine. Cause: release staging
  copied only the Unity player + bats; `Paths.GetPath(GamePaths.Config|Data)` is CWD-relative
  (`Start_SS3D_*.bat` / smoke `cd` beside the exe). Fix: `seed_cwd_data` in
  `develop-release.yml` from tracked `Builds/Game/Config` + `Builds/Game/Data/Tilemaps`.
  Do not ship `Data/ServerMeta/` (gitignored runtime envelope preferred over `permissions.txt`).
- **Host.bat must give the local client a loopback address.** `ResetOnBuiltApplication` clears
  `ServerAddress`; Host without `-ip=` used to DNS-resolve empty → `fe80::…` and drop the
  session after WorldReady (Wine reproduces hard). See networking-session pitfalls; Host.bat
  now passes `-ip=127.0.0.1` and code defaults empty Host address to loopback.
- **License secrets live on the `unity_tests` Environment.** Jobs without
  `environment: unity_tests` see empty `UNITY_LICENSE` / `UNITY_SERIAL` even when EditMode is
  green (the old `main.yml` hit this before Environment was wired).
- **Do not use `versioning: Semantic`** until tags are clean `X.Y.Z` — letter-suffix tags produce
  `Failed to parse git describe output`.
- **Do not use `ClientBuildScript` for Windows** — it hardcodes `StandaloneLinux64`; Windows
  uses the default game-ci build method.
- **Default manual prereleases are unproven by EditMode/smoke/Linux.** Trust PR/`develop`
  EditMode CI and opt into `build_linux` + `run_smoke` (or `multiplayer-smoke-test.yml`)
  when you need a gated cut. Nightly includes Linux binaries but still skips EditMode/smoke.
- **Windows is not multiplayer-smoke-tested.** Even with `run_smoke`, trust is “same commit as
  green Linux smoke.”
- **Unity 6 Linux binary names have no `.x86_64` suffix.** Smoke/`chmod` must use `SS3D-Server`
  / `SS3D-Client` (local Editor menus still write `SS3D.x86_64` via hardcoded paths).
- **Skipped optional jobs cascade unless dependents use `always()`.** EditMode and Linux are
  often skipped on manual dispatch; every downstream job (`build-windows`, `release`) must use
  `always()` and then check `needs.*.result == 'success' || 'skipped'` as appropriate.
- **Ghost workflows after rename/delete stay `active` until disabled.** Deleting YAML is not
  enough — `gh workflow disable <id|path>` or the Actions UI. `testrunner.yml` and
  `AddToRoadmap.yml` were disabled after their files were removed.
- **Offline TomNAS queues forever without a probe.** `prepare` picks GitHub-hosted when
  `TomNAS-unity` is not `online` (job `timeout-minutes` does not cover queue wait).

## Out of scope

- Auto-trigger on every `develop` push (nightly schedule only).
- Windows dedicated-server build / Windows smoke harness.
- Wiring `known_unity_noise` into the harness fail gate.

## Related docs

- [2026-07_multiplayer-test-harness.md](2026-07_multiplayer-test-harness.md)
- [2026-07_headless-dedicated-server.md](2026-07_headless-dedicated-server.md)
- [2026-07_multiplayer-testing-self-hosted-ci.md](2026-07_multiplayer-testing-self-hosted-ci.md)
- [networking-session.md](systems/networking-session.md)
