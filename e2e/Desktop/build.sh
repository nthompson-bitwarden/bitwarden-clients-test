#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

npm run build-native --workspace @bitwarden/desktop
exec npm run build --workspace @bitwarden/desktop -- "$@"
