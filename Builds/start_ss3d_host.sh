#!/usr/bin/env bash
# Hosts a game on this machine (server + your own client). Others join with start_ss3d_client.sh.
# Usage: ./start_ss3d_host.sh [name] [port] [extra args...]
# The default name, hostingUser, is ServerOwner in Game/Config/permissions.txt.
set -euo pipefail

CKEY="${1:-hostingUser}"
PORT="${2:-1151}"
EXTRA_ARGS=("${@:3}")

cd "$(dirname "$0")/Game"
# Local builds are SS3D.x86_64; release zips ship SS3D-Client.
BIN=./SS3D.x86_64
[ -e "$BIN" ] || BIN=./SS3D-Client
chmod +x "$BIN" 2>/dev/null || true

exec "$BIN" -ip=127.0.0.1 -host -port="$PORT" -ckey="$CKEY" -skipintro "${EXTRA_ARGS[@]}"
