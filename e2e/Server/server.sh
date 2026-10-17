#!/usr/bin/env bash
set -euo pipefail

SERVER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export E2E_SERVER_DIR="$SERVER_DIR"
export E2E_STATE_DIR="$SERVER_DIR/.state"
# Pin source and seeder together. Override before the first up to test another commit/tag.
SERVER_REF="${E2E_SERVER_REF:-9129efbd1531d5d8472345638efad7ef25a7337a}"
PROJECT="${E2E_COMPOSE_PROJECT:-bitwarden-e2e}"
command="${1:-up}"
if (( $# )); then shift; fi

usage() {
  cat <<'EOF'
Usage: ./e2e/Server/server.sh [command]
  up                Clone, build, migrate, CLI-seed, and wait for the full server
  seed [CLI args]    Run default seed once, or pass arguments to SeederUtility
  api               Build and start the optional Seeder API
  down              Stop the stack, preserving test data
  reset             Stop the stack and DELETE this stack's database and vault volumes
  status            Show containers
  verify            Check password login and vault sync for the pre-seeded accounts
  logs [services]   Follow container logs
  config            Validate and print the Compose model (includes local secrets)
  help              Show this help
EOF
}

case "$command" in
  help|-h|--help) usage; exit 0 ;;
  up|seed|api|down|reset|status|verify|logs|config) ;;
  *) usage >&2; exit 2 ;;
esac
for executable in docker git python3 openssl; do
  command -v "$executable" >/dev/null || { echo "Missing prerequisite: $executable" >&2; exit 1; }
done
docker compose version >/dev/null
mkdir -p "$E2E_STATE_DIR"
# Prevent concurrent clone/config/seed/reset operations on this installation.
command -v flock >/dev/null || { echo 'Missing prerequisite: flock (util-linux)' >&2; exit 1; }
case "$command" in
  up|seed|api|down|reset)
    exec 9>"$E2E_STATE_DIR/lock"
    flock -n 9 || { echo 'Another server.sh command is running.' >&2; exit 1; }
    ;;
esac

compose() {
  docker compose --project-name "$PROJECT" --env-file "$E2E_STATE_DIR/compose.env" \
    --file "$SERVER_DIR/compose.yml" "$@"
}
if [[ "$command" != up && ! -f "$E2E_STATE_DIR/compose.env" ]]; then
  echo 'No local server installation. Run server.sh up first.' >&2
  exit 1
fi

seed() {
  if (( $# )); then
    compose run --rm --build -T seeder-cli "$@"
  else
    compose run --rm --build -T --entrypoint /bin/bash seeder-cli /seed-default.sh
  fi
}

case "$command" in
  up)
    python3 "$SERVER_DIR/configure.py" "$E2E_STATE_DIR"
    if [[ ! -d "$E2E_STATE_DIR/server/.git" ]]; then
      git clone --no-checkout https://github.com/bitwarden/server.git "$E2E_STATE_DIR/server"
      git -C "$E2E_STATE_DIR/server" checkout --detach "$SERVER_REF"
    fi
    # Never reset or pull a reused checkout: local server changes remain available for testing.
    git -C "$E2E_STATE_DIR/server" rev-parse HEAD > "$E2E_STATE_DIR/server-revision.txt"
    compose up --detach --build --wait --wait-timeout 600
    seed
    python3 "$SERVER_DIR/verify.py" "$E2E_STATE_DIR/test-environment.json"
    echo "Server ready. Test URLs and seeded credentials: $E2E_STATE_DIR/test-environment.json"
    ;;
  seed) seed "$@" ;;
  api) compose --profile seeder-api up --detach --build --wait --wait-timeout 600 seeder-api ;;
  down) compose --profile seeder-api down ;;
  reset) compose --profile seeder-api down --volumes --remove-orphans ;;
  status) compose --profile seeder-api ps --all ;;
  verify) python3 "$SERVER_DIR/verify.py" "$E2E_STATE_DIR/test-environment.json" ;;
  logs) compose --profile seeder-api logs --follow --tail 100 "$@" ;;
  config) compose --profile seeder-api config ;;
esac
