#!/usr/bin/env bash
set -euo pipefail

# This marker lives with the database's encryption keys, not in the source checkout.
# down/up preserves it; reset removes both named volumes and therefore reseeds.
core_dir="${E2E_CORE_DIR:-/etc/bitwarden/core}"
marker="$core_dir/e2e-seed-complete"
if [[ -f "$marker" ]]; then
  echo 'Default CLI seed already completed. Use reset for a fresh database.'
  exit 0
fi
if [[ -f "$core_dir/e2e-seed-started" ]]; then
  echo 'A previous seed failed partway through. Run server.sh reset, then server.sh up.' >&2
  exit 1
fi
touch "$core_dir/e2e-seed-started"
for client in browser desktop web; do
  # Supplying names avoids the CLI's automatic email mangling for anonymous users.
  dotnet /app/SeederUtility.dll individual --subscription free \
    --first-name E2E --last-name "$client" --email "e2e-$client@example.com" \
    --password "$E2E_SEED_PASSWORD" --kdf-iterations 600000 --vault
done
touch "$marker"
echo 'Seeded Browser, Desktop, and Web accounts with personal ciphers and folders.'
