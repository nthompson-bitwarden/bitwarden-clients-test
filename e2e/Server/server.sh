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
  up                Clone, build, migrate, start Seeder API, and verify the server
  seed [CLI args]    Run an explicit SeederUtility command
  api               Build and start the Seeder API
  down              Stop the stack, preserving test data
  reset             Stop the stack and DELETE this stack's database and vault volumes
  status            Show containers
  verify            Verify temporary Seeder API account login, vault sync, and cleanup
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
    echo 'Pass SeederUtility arguments, for example: server.sh seed preset --list' >&2
    return 2
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
    # nginx resolves service names at startup; refresh routes after containers change addresses.
    compose up --detach --no-deps --force-recreate --wait --wait-timeout 120 nginx
    python3 "$SERVER_DIR/verify.py" "$E2E_STATE_DIR/test-environment.json"
    echo "Server ready. Test service configuration: $E2E_STATE_DIR/test-environment.json"
    ;;
  seed) seed "$@" ;;
  api) compose up --detach --build --wait --wait-timeout 600 seeder-api ;;
  down) compose down ;;
  reset) compose down --volumes --remove-orphans ;;
  status) compose ps --all ;;
  verify) python3 "$SERVER_DIR/verify.py" "$E2E_STATE_DIR/test-environment.json" ;;
  logs) compose logs --follow --tail 100 "$@" ;;
  config) compose config ;;
esac
