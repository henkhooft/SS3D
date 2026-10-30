#!/usr/bin/env bash
# Compile → EditMode → visual → lifecycle [→ client build if stale → remote client] in the open editor.
# Usage: Testing/ui/validate.sh [--quick|--full|--steps a,b]. Run outside the agent Bash sandbox (X11 watchdog).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
exec artifacts/unity-mcp/venv/bin/python Testing/ui/validate.py "$@"
