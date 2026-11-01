#!/usr/bin/env bash
# Update an extracted Linux client release. Requires Python 3; never launches the game.
set -euo pipefail
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec python3 "$SCRIPT_DIR/update_ss3d.py" "$@"
