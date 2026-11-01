# SS3D launcher / updater

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
