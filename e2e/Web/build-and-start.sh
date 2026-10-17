#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

# webpack builds before serving and stays in the foreground for the test runner.
if [[ -n "${E2E_SERVER_URL:-}" ]]; then
  exec npm exec -- webpack serve --config "$REPO_ROOT/e2e/Web/webpack.config.cjs" "$@"
fi
exec npm run build:bit:dev:watch --workspace @bitwarden/web-vault -- "$@"
