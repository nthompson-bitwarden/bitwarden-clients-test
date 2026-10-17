# Local e2e server

From the repository root:

```bash
./e2e/Server/server.sh up
```

Requires Linux (or WSL), Docker with Compose v2+, Git, Python 3.10+, OpenSSL,
and `flock` from util-linux. SQL Server requires an amd64 host or amd64 emulation.
The first run downloads and builds the upstream server and Rust-backed seeder;
allow several minutes and sufficient Docker memory for SQL Server and parallel builds.
No host .NET SDK, production account, or cloud database is needed.

`up` clones `bitwarden/server` into ignored `.state/server`, checks out a pinned
commit, builds its standard server containers, waits for Admin to migrate SQL Server,
starts the other services, and runs **SeederUtility** before reporting success.
It then verifies password login and vault sync for all three seeded accounts.
The stack includes API, Identity, Admin, SSO, SCIM, events, notifications, icons,
attachments, a released web vault, and a local SMTP mail catcher. Its network,
container names, and database/key volumes are separate from other installations.

The default endpoints are:

| Service | URL |
| --- | --- |
| Web vault / self-hosted server | `http://localhost:18090` |
| API | `http://localhost:18090/api` |
| Identity | `http://localhost:18090/identity` |
| Captured email | `http://localhost:18091` |
| Optional Seeder API | `http://localhost:18092` |

Host ports bind to loopback. HTTP on localhost provides a browser secure context;
use this installation from local clients. Remote devices need a trusted HTTPS
proxy and matching server URL configuration.

## Pre-seeded accounts

Startup invokes the CLI's `individual --subscription free --vault` command for
each client, with PBKDF2 set to 600,000 iterations. Each account starts with roughly
75 personal vault items and folders; generated item contents may vary between resets.

| Client | Email | Default master password |
| --- | --- | --- |
| Browser | `e2e-browser@example.com` | `E2e-Test-Password-123!` |
| Desktop | `e2e-desktop@example.com` | `E2e-Test-Password-123!` |
| Web | `e2e-web@example.com` | `E2e-Test-Password-123!` |

Tests can read `.state/test-environment.json` for the actual server URL and
credentials. This file is ready for use **after `up` succeeds**. Tests do not need
a Seeder API client. The CLI and server share database settings, attachment storage,
and ASP.NET Data Protection keys so vault contents are encrypted for real client use.

Default seeding runs once per database. Repeated `up` or `seed` preserves accounts
and test changes. If seeding fails halfway through, startup fails and asks for a
reset rather than duplicating accounts. For a clean run:

```bash
./e2e/Server/server.sh reset # Deletes this stack's database and core/key volumes
./e2e/Server/server.sh up
```

To add data with any upstream CLI command, pass its arguments directly:

```bash
./e2e/Server/server.sh seed preset --list
./e2e/Server/server.sh seed preset --name dev.playground
./e2e/Server/server.sh seed individual --subscription free \
  --first-name Test --last-name Extra --email extra@example.com --vault
```

These commands run against the same database and do not clear existing data.
Use unique identities or the upstream `--mangle` flag when repeatedly adding data.
Premium and organization license scenarios require the upstream development signing
certificate/configuration; the default free accounts do not require licenses.

## Optional Seeder API

```bash
./e2e/Server/server.sh api
```

The API stays off during normal startup. When enabled, it shares the same database,
keys, and SQL-backed distributed cache as the server. Its Basic Auth username and
generated password are in `test-environment.json`. Use upstream `/seed` and `/query`
endpoints with an `X-Play-Id` header for per-run cleanup. No API wrapper is needed
for the default CLI-seeded accounts.

## Configuration and lifecycle

Before the **first** `up`, optional environment variables are:

- `E2E_SERVER_PORT`, `E2E_MAIL_PORT`, `E2E_SEEDER_PORT`: default `18090`, `18091`, `18092`.
- `E2E_SEED_PASSWORD`: override the default test master password.
- `E2E_SERVER_REF`: server commit/tag to check out; default is pinned in `server.sh`.
- `E2E_WEB_IMAGE`: released web image; default `ghcr.io/bitwarden/web:2026.9.0`.
- `BW_INSTALLATION_ID`, `BW_INSTALLATION_KEY`: optional real installation credentials.
  Local generated placeholders support offline tests; cloud-dependent functionality
  such as push relay or license synchronization needs real installation credentials.

Settings persist in ignored `.state/settings.json` so restarts retain database
passwords and signing keys. To change saved settings, stop the stack, edit that
file, and run `up`; changing database or seed passwords requires `reset`. Keep the
generated signing-certificate password unchanged. The checkout is
preserved without automatic pulls/resets, so local server edits can be tested.
Its actual commit is recorded in `.state/server-revision.txt`.

```bash
./e2e/Server/server.sh status
./e2e/Server/server.sh verify # Repeat password-login and vault-sync smoke checks
./e2e/Server/server.sh logs api identity
./e2e/Server/server.sh down # Preserves database, seeded data, and encryption keys
```

Upstream references: [Seeder CLI](https://github.com/bitwarden/server/tree/main/util/SeederUtility),
[Seeder API](https://github.com/bitwarden/server/tree/main/util/SeederApi),
[standard self-hosted services](https://github.com/bitwarden/server/blob/main/util/Setup/Templates/DockerCompose.hbs).
