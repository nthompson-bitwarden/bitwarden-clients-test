# Local e2e server

From the repository root:

```bash
./e2e/Server/server.sh up
```

Supported hosts are macOS (Apple Silicon or Intel) and Linux/WSL. Install:

- Docker Desktop on macOS, or Docker Engine on Linux/WSL, with Compose v2+ and Buildx.
  Start Docker and select Linux containers before running the command.
- Git, Python 3.10+, OpenSSL, and Bash. No host .NET SDK, Rust toolchain, Node,
  `flock`, or pre-existing server checkout is needed for server startup.
- On ARM64, working AMD64 container emulation for SQL Server. Application and
  Seeder containers build natively for the Docker host architecture. Docker
  Desktop must have AMD64 emulation enabled; Linux ARM64 needs equivalent binfmt
  support configured by the machine administrator.

The first run downloads source, images, and build dependencies. Allow several
minutes, adequate Docker disk space, and sufficient Docker memory. Startup builds
at most two application images at a time to avoid running all server compilers
concurrently in a developer Docker VM. Prerequisite checks run before installation
configuration is generated; missing tools and an
unavailable daemon produce actionable errors.

`up` fetches the pinned `bitwarden/server` revision into a staging directory and
only publishes a complete checkout as ignored `.state/server`. It validates the
required source projects, builds their containers, and waits for Admin to migrate SQL Server before
starting the other services. Seeder API is included in normal startup. Verification
creates a temporary account and encrypted vault through Seeder API, checks real
password login and sync, then deletes the play's data. Only successful verification
atomically publishes `.state/test-environment.json`. A failed `up` removes any old
readiness manifest; generated settings, certificates, and database data remain
available for a retry.

The stack includes API, Identity, Admin, SSO, SCIM, events, notifications, icons,
attachments, a released web vault, and a local SMTP mail catcher. Its network and
volumes are separate from other installations.

| Service                        | Default URL                       |
| ------------------------------ | --------------------------------- |
| Web vault / self-hosted server | `http://localhost:18090`          |
| API                            | `http://localhost:18090/api`      |
| Identity                       | `http://localhost:18090/identity` |
| Captured email                 | `http://localhost:18091`          |
| Seeder API                     | `http://localhost:18092`          |

Host ports bind to loopback. HTTP on localhost provides a browser secure context.
Remote devices need a trusted HTTPS proxy and matching server URL configuration.

## Per-test data

The [shared Seeder API client](../SeederApi/README.md) creates an isolated account
and encrypted vault data for each UI test. There are no persistent default test
accounts. Tests read service URLs and Seeder API credentials from
`.state/test-environment.json`, available after `up` succeeds. Generated credentials
and decrypted key material must not appear in source or reports.

The client sends a unique `X-Play-Id` for each test and deletes that play during
teardown. Seeder API shares the server's database, attachment storage, ASP.NET Data
Protection keys, and SQL-backed persistent cache. The shared cache is required for
Email two-factor code queries.

Existing databases can be reused without a reset. Old CLI-seeded accounts and
markers are ignored and left intact. `up` and `verify` no longer depend on them.
For a deliberately clean installation:

```bash
./e2e/Server/server.sh reset # Deletes this stack's database and core/key volumes
./e2e/Server/server.sh up
```

## Manual CLI seeding

Explicit upstream CLI commands remain available independently of test provisioning:

```bash
./e2e/Server/server.sh seed preset --list
./e2e/Server/server.sh seed individual --subscription free \
  --first-name Test --last-name Extra --email extra@example.com --vault
```

These commands do not clear existing data. Use unique identities or `--mangle`
when adding data repeatedly. Premium and organization license scenarios require
the upstream development signing certificate/configuration; normal tests use free
accounts.

## Configuration and lifecycle

Before the **first** `up`, optional environment variables are:

- `E2E_SERVER_PORT`, `E2E_MAIL_PORT`, `E2E_SEEDER_PORT`: default `18090`, `18091`, `18092`.
  These ports and the development Web port must be distinct and in the range 1–65535.
- `E2E_WEB_PORT`: development vault port, default `18080`; its HTTPS localhost
  URL is the configured vault URL, providing the HTTPS AppID required by WebAuthn.
  Both vault origins are allowed, and API/Identity retain their backend URLs. Use the same port
  in the test runner and Web configuration. It is persisted with the other ports;
  edit `settings.json` and rerun `up` when changing it.
- `E2E_SERVER_REF`: server commit/tag; the default is pinned in `server.py`.
- `E2E_WEB_IMAGE`: released web image; default `ghcr.io/bitwarden/web:2026.9.0`.
- `BW_INSTALLATION_ID`, `BW_INSTALLATION_KEY`: real installation credentials for
  cloud-dependent functionality. Local placeholders support offline tests.

`E2E_STATE_DIR` optionally selects an absolute or working-directory-relative state
directory; it defaults to `e2e/Server/.state`. `E2E_COMPOSE_PROJECT` selects the
Docker project name, default `bitwarden-e2e`. Use the same pair for every lifecycle
command. Separate installations need separate state directories, project names,
and host ports. State contains secrets: keep custom locations private and ignored.
Concurrent mutating commands and verification are locked per state directory using
Python's standard library on both macOS and Linux.

Settings persist in ignored `.state/settings.json`. To change saved settings,
stop the stack, edit that file, and run `up`. Changing database credentials requires
a reset. Preserve the generated signing-certificate password. The server checkout
is reused without automatic pulls/resets; its commit is recorded in
`.state/server-revision.txt`.

```bash
./e2e/Server/server.sh api    # Build/start Seeder API independently
./e2e/Server/server.sh status
./e2e/Server/server.sh verify # Temporary provisioning/login/sync/cleanup smoke check
./e2e/Server/server.sh logs api identity seeder-api
./e2e/Server/server.sh down   # Preserves database and encryption keys
```

## Recovery and validation

If startup fails, read its error, then use `status` and service-specific `logs`.
Fix the reported dependency, port, source, build, or health problem and rerun `up`.
A retry reuses generated credentials, the signing certificate, the completed
server checkout, and existing Docker volumes. It does not reset a database or
replace a developer checkout. An incomplete existing `.state/server` is rejected:
preserve local edits and move that directory aside before retrying. An interrupted
initial fetch may leave `.server-clone-*` staging directories; these are not reused
and can be removed after ensuring no startup command is running.

Do not run `reset` to recover a build failure. It explicitly deletes this project's
database and vault volumes and removes the readiness manifest. `config` intentionally
prints local secrets; do not paste its output into reports.

Run bootstrap unit tests without Docker:

```bash
python3 -m unittest discover -s e2e/Server/tests -v
```

For a genuine first-install check, use a new state directory under ignored
`e2e/TestResults/`, a unique project name, and three unused host ports. Run `up`,
`verify`, `up` again, `down`, then `up` and `verify` again. Keep the same environment
for all commands. Finish with `reset` for that isolated project only. A source or
unit-test pass does not establish container startup or provisioning acceptance.
