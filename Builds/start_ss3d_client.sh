#!/usr/bin/env bash
# Joins an SS3D server with the Linux client.
# Usage: ./start_ss3d_client.sh [server_ip] [port] [name] [extra args...]
# Run with no arguments to join 127.0.0.1:1151 as your login name.
set -euo pipefail

SERVER_IP="${1:-127.0.0.1}"
PORT="${2:-1151}"
CKEY="${3:-${USER:-player}}"
EXTRA_ARGS=("${@:4}")

cd "$(dirname "$0")/Game"
# Local builds are SS3D.x86_64; release zips ship SS3D-Client.
BIN=./SS3D.x86_64
[ -e "$BIN" ] || BIN=./SS3D-Client
chmod +x "$BIN" 2>/dev/null || true

exec "$BIN" -ip="$SERVER_IP" -port="$PORT" -ckey="$CKEY" -skipintro "${EXTRA_ARGS[@]}"
