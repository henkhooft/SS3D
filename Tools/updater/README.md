# SS3D launcher / updater

## Update an extracted Windows release

Release zips ship `Builds/Update_SS3D.bat` and `Builds/Update_SS3D.ps1` beside the launch
scripts. Double-click `Update_SS3D.bat` with the game closed, or pass `-CheckOnly` to check
without installing. This updates the existing folder, preserving `Game/Config`, custom and
modified maps, exports, server state and logs. Player settings outside the install are untouched.
Unmodified bundled maps refresh using hashes recorded in `.ss3d-release.json`; older downloads
without metadata conservatively keep all existing maps, including collisions with bundled maps.

Only **published, non-prerelease `vMAJOR.MINOR.PATCH` releases** are selected, by highest version.
Drafts, prereleases and nightlies never reach this channel. CI still creates prereleases: test
one manually, then promote it in GitHub by clearing its prerelease flag. The updater will then
offer that exact build. A newer installed version is never automatically downgraded.

The script locks out concurrent updates, refuses while any SS3D process is running, verifies
GitHub's SHA-256 asset digest, validates archive paths and required player files, and stages
on the same filesystem before swapping files. A caught swap failure restores the original.
If restoration itself fails or power is lost, staging and backup directories remain for recovery.
There is no dependency on Git, Unity, a hosted server, or an administrator account.

Each successful update keeps the previous complete installation in `.ss3d-backups/<id>/`.
To roll back, close the game and run the Host/Client script in that backup; it contains the
local data from before the update. Later changes remain in the newer installation. Backups
are never pruned automatically. The updater files themselves stay as a stable bootstrap.

Regression coverage: `pwsh -NoProfile -File Tools/updater/test-update.ps1` (or `powershell`
on Windows). All fixtures and simulated downloads are temporary; no real game is launched.
`.github/workflows/updater-tests.yml` runs it on Windows PowerShell 5.1 for relevant PRs;
the release workflow also gates publication on these tests. A real Windows download/update
and backup launch still need a manual check before announcing the first release.

## Separate bootstrap launchers

Bootstrap launchers that download and run the newest **stable `v*`** release. They do not
require a git checkout and do not update an install in place: every release is extracted
under a versioned directory, and the current plus one previous version are kept.

Only tags matching `vMAJOR.MINOR.PATCH` are considered. `develop-*`, `develop-nightly` and
other development cuts are ignored. Downloads are verified against GitHub's SHA-256 asset
digest; if the digest is missing or does not match, the launcher refuses to install.

The launcher itself is a small, stable bootstrap. It is not replaced by game updates.

## Linux

Requirements: `curl`, `jq`, `unzip`, `sha256sum`.

```bash
./ss3d-launcher.sh --server <host> --port 1151 --ckey <ckey>
```

Installs under `~/.local/share/ss3d/versions/`. Useful flags:

- `--server HOST`, `--port PORT`, `--ckey CKEY` — passed to the game.
- `--install-dir DIR` — override the install root.
- `--tag TAG` — testing only; follow a specific release instead of the stable channel.
- `--no-launch` — download/install only.
- `--` — remaining arguments are passed to the game.

## Windows

Run `SS3D-Launcher.bat` (it invokes PowerShell with `-ExecutionPolicy Bypass`). Windows
PowerShell 5.1 or newer is required, and `SS3D-Launcher.ps1` must stay next to the `.bat`.

```bat
SS3D-Launcher.bat -Server <host> -Port 1151 -Ckey <ckey>
```

Installs under `%LOCALAPPDATA%\SS3D\versions\`. Parameters: `-InstallDir`, `-Tag`,
`-NoLaunch`.

## Notes

- The default server is `127.0.0.1`; pass the hosted address when one is configured.
- If GitHub is unreachable, the launcher starts the already-installed version when one
  exists; otherwise it exits with an error.
- The launchers check GitHub's releases API on each start. The API is public and
  rate-limited; one check per launch is well within the unauthenticated limit.
- `--tag` / `-Tag` is for testing a specific release. Normal use follows the stable channel.
