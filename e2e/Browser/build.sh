#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

browser=chrome
if (( $# )) && [[ "$1" != -* ]]; then
  browser="$1"
  shift
fi

case "$browser" in
  chrome|edge|firefox|opera|safari) ;;
  *)
    echo "Unsupported browser: $browser. Expected chrome, edge, firefox, opera, or safari." >&2
    exit 2
    ;;
esac

exec npm run "build:bit:$browser" --workspace @bitwarden/browser -- "$@"
