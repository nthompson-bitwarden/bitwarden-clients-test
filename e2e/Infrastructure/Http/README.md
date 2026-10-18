# Shared API transport

`ApiHttpClient` owns HTTP mechanics for all C# e2e API calls: base URLs, a
30-second timeout, JSON response parsing, cancellation, safe HTTP/JSON failures,
and response disposal. It accepts an optional `HttpMessageHandler` for isolated
contract tests. Requests can carry JSON or form content and per-request headers.

Keep endpoint paths and authentication in API clients:

- `SeederApi/SeederApiClient`: Basic authentication, seed/query contracts, and
  play-scoped deletion. `SeederSession` owns provisioning and teardown.
- `ServerApi/ServerApiClient`: password-token authentication through identity,
  then Bearer authentication and the current play ID for internal API calls.
- `Authentication/TwoFactorEnrollment`: Email, TOTP, and WebAuthn enrollment
  workflows using the server client and the current Seeder session.
- `Authentication/VirtualAuthenticator`: Chromium CDP authenticator lifecycle
  attached to the current test's Dramaturge page.

Web, Browser, and Desktop share one assembly and can import `e2e.Authentication`.
Keep enrollment helpers and API clients per test, and dispose them during
teardown. Do not share mutable authorization headers between tests or services.
The existing Web two-factor fixtures show enrollment setup and disposal.

Callers own and dispose request messages. `SendAsync` checks success and ignores
the body; `SendJsonAsync<T>` requires a JSON result unless empty responses are
explicitly allowed. Both dispose responses on success or failure. Exceptions
include a safe operation label and HTTP status or parsing outcome, without
response bodies or JSON exception details. Operation labels must not contain
credentials or vault data. Mutations are never automatically retried.

`SendRawAsync` returns a caller-owned response without checking status. The Web
onboarding proxy uses this to forward unsuccessful responses unchanged, while
transforming selected successful JSON responses in its Web-specific helper.

Future public API clients should compose this transport with their own endpoint
paths and authentication. No public API endpoints or API-key flow are added here.
