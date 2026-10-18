#!/usr/bin/env bash
set -euo pipefail

SERVER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
command -v python3 >/dev/null || { echo 'Missing prerequisite: Python 3.10+.' >&2; exit 1; }
exec python3 "$SERVER_DIR/server.py" "$@"
