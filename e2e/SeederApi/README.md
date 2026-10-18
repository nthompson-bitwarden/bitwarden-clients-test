# Seeder API test client

This directory owns the C# client for the local Bitwarden Seeder API. It is part of
`e2e.csproj` and can be used by Web, Browser, and Desktop fixtures.

Start the backend with `./e2e/Server/server.sh up`. Seeder API is included in normal
startup. Service URLs and Basic Auth credentials are read from the ignored
`Server/.state/test-environment.json`; never copy its contents into test output.

Client base classes expose `Seeds` and `Account`. Every test owns a `SeederSession`
with a unique play ID, an email-verified free account, and one encrypted folder and
login item. The account email is resolved through the API's mangle map. Setup
creates the account before injecting account-specific browser state. Teardown
removes only that play's data, including after partial provisioning failures.

```csharp
//Arrange
var folderId = await Seeds.CreateFolderAsync("Imported items");
await Seeds.CreateLoginAsync("Example login", folderId);
```

For additional scenes and queries, use `Seeds.SeedAsync<T>` and
`Seeds.QueryAsync<T>`; these automatically use the session's play ID.
The underlying `SeederApiClient` is also available for standalone consumers. Mutations are never
automatically retried. A bounded Email code query is available through
`Seeds.GetEmailCodeAsync()`; it reads the actual code issued by the server from the
shared persistent cache without consuming it.

The shared `Authentication/TwoFactorEnrollment` helper lets Web, Browser, and Desktop
fixtures enroll factors through real server management endpoints,
since the exposed seed scenes do not accept two-factor provider settings. Email
uses the code query, TOTP uses the issued secret, and WebAuthn uses a Chromium
virtual authenticator from `Authentication/VirtualAuthenticator`. These helpers accept
the current test's Dramaturge page and are independent of Web page objects. Chromium
CDP is required for the virtual authenticator; Firefox and Safari skip the WebAuthn test explicitly.

`SeederApiClient` uses the shared [HTTP transport](../Infrastructure/Http/README.md).
Its Basic authentication remains separate from the Bearer authentication used by
`ServerApi/ServerApiClient` for internal management endpoints.

HTTP transport and API-client contract tests in `Infrastructure/Tests/` use an in-memory handler and run
with `--filter TestCategory=Infrastructure`. UI tests exercise the live services.
If a runner is forcibly terminated before teardown, its play may remain; use the
Seeder API's specific play deletion endpoint for that known play rather than
bulk-deleting unrelated test data.
