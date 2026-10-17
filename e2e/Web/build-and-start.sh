#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

# webpack builds before serving and stays in the foreground until stopped manually.
exec npm run build:bit:dev:watch --workspace @bitwarden/web-vault -- "$@"
