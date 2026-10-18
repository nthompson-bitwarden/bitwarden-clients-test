# Local e2e server

From the repository root:

```bash
./e2e/Server/server.sh up
```

Requires Linux (or WSL), Docker with Compose v2+, Git, Python 3.10+, OpenSSL,
and `flock` from util-linux. SQL Server requires an amd64 host or amd64 emulation.
The first run builds the pinned upstream server and Rust-backed Seeder API; allow
several minutes and sufficient Docker memory.

`up` clones `bitwarden/server` into ignored `.state/server`, checks out the pinned
commit, builds its containers, and waits for Admin to migrate SQL Server before
starting the other services. Seeder API is included in normal startup. Verification
creates a temporary account and encrypted vault through Seeder API, checks real
password login and sync, then deletes the play's data.

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
- `E2E_WEB_PORT`: development vault port, default `18080`; its HTTPS localhost
  URL is the configured vault URL, providing the HTTPS AppID required by WebAuthn.
  Both vault origins are allowed, and API/Identity retain their backend URLs. Use the same port
  in the test runner and Web configuration, and rerun `up` when changing it.
- `E2E_SERVER_REF`: server commit/tag; the default is pinned in `server.sh`.
- `E2E_WEB_IMAGE`: released web image; default `ghcr.io/bitwarden/web:2026.9.0`.
- `BW_INSTALLATION_ID`, `BW_INSTALLATION_KEY`: real installation credentials for
  cloud-dependent functionality. Local placeholders support offline tests.

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
