# Client builds for test runs

These Bash entry points use the npm workspace commands in the parent repository.
They can be invoked from any working directory. Install the repository's Node/npm
dependencies with `npm ci` first. Desktop also requires the native build
prerequisites used by `apps/desktop/desktop_native` (Rust and platform libraries).

Run the Web script from a terminal with Node/npm on `PATH`. With NVM, install the
version in `.nvmrc` before installing dependencies. Web tests do not need npm on
the test runner's `PATH`.

From the repository root:

| Script                             | Result                                                                                               |
| ---------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `./e2e/Web/build-and-start.sh`     | Builds and serves the licensed development web vault at `https://localhost:8080` by default.         |
| `./e2e/Browser/build.sh [browser]` | Builds the licensed extension for the selected browser in `apps/browser/build` (default: Chrome).    |
| `./e2e/Desktop/build.sh`           | Builds the native module, then Electron main, renderer, and preload bundles in `apps/desktop/build`. |

Build scripts return a nonzero exit code on failure. They do not install
dependencies or launch the browser/desktop clients. Additional arguments are
forwarded to the final npm build command, for example
`./e2e/Web/build-and-start.sh --port 8081`.

The browser target can be `chrome`, `edge`, `firefox`, `opera`, or `safari`:

```bash
./e2e/Browser/build.sh firefox
./e2e/Browser/build.sh edge --watch
```

Each build writes to the same output directory, so build browser targets sequentially.

The Web script stays in the foreground and watches for changes. Start it manually
and wait for successful compilation before running Web tests. Tests do not start,
build, probe readiness, or stop either the Web server or its backend.

The Web script always uses the standard licensed development workspace command
and the existing `apps/web/config` settings, including `local.json` overrides.

## Running Web tests

Start the [seeded backend](Server/README.md) from the repository root:

```bash
./e2e/Server/server.sh up
```

Merge the following settings into the ignored `apps/web/config/local.json` to
route the development client to the seeded backend. Preserve any other local
settings you need. The proxy targets include the self-hosted path prefixes because
the dev proxy strips those prefixes from incoming requests.

```json
{
  "urls": {
    "base": "https://localhost:18080",
    "webVault": "https://localhost:18080",
    "api": "https://localhost:18080/api",
    "identity": "https://localhost:18080/identity",
    "notifications": "https://localhost:18080/notifications",
    "events": "https://localhost:18080/events",
    "icons": "https://localhost:18080/icons",
    "scim": "https://localhost:18080/scim"
  },
  "dev": {
    "port": 18080,
    "proxyApi": "http://localhost:18090/api",
    "proxyIdentity": "http://localhost:18090/identity",
    "proxyNotifications": "http://localhost:18090/notifications",
    "proxyEvents": "http://localhost:18090/events",
    "proxyIcons": "http://localhost:18090/icons",
    "wsConnectSrc": "wss://localhost:18080"
  }
}
```

Start Web in a separate terminal and wait for successful compilation:

```bash
./e2e/Web/build-and-start.sh
```

Use the backend's actual URL in the proxy targets if its port was customized. Then run the tests from
Rider or with `dotnet test e2e/e2e.csproj --filter FullyQualifiedName~e2e.Web.Tests`.
Tests read credentials from `Server/.state/test-environment.json`; a missing
manifest reports the backend startup command. Both servers stay running after tests.

Web tests open `https://localhost:18080` by default. To use another port, set
`E2E_WEB_PORT` in the test runner and update the web URLs, `dev.port`, and
`dev.wsConnectSrc` in `local.json` to match. An unavailable Web server fails normal page
navigation; the tests do not retry server startup or wait for compilation.

## NUnit base classes

Tests in each client directory inherit its `BaseTest`:

```csharp
using NUnit.Framework;

namespace e2e.Web.Tests;

public class AuthenticationTests : e2e.Web.BaseTest
{
    [Test]
    public async Task ShowsLogin()
    {
        await Page.GetByLabel("Email address").FillAsync(Account.Email);
        // Page is the built client; ServerUrl and Account come from the CLI seed.
    }
}
```

Use `e2e.Browser.BaseTest` for the extension and `e2e.Desktop.BaseTest` for Electron.
The client base classes provide `Page`, `Group`, `ServerUrl`, and the respective
seeded `Account.Email` / `Account.Password`. Web inherits Dramaturge NUnit's `PageTest`;
Browser and Desktop inherit the shared `e2e.BaseTest`. Client base classes make fixtures parallelizable and create an instance per test case.
Keep mutable state isolated per test; the runtime synchronizes shared build preparation.

For Browser and Desktop, the runtime verifies all three seeded server logins and vaults.
If that check fails or the server manifest is missing, it runs `Server/server.sh up`
and waits for pre-seeding to finish. Each of those client builds runs once per test process.

- **Web:** reads the existing seeded-account manifest and navigates to the manually
  started Web server. Dramaturge `PageTest` opens an isolated browser
  and page per test within the run's shared browser group, configured to accept the
  local development certificate. Web page/component objects own login interactions
  and expose locators; tests own assertions and vault readiness expectations.
  Before navigation, Web tests inject account-specific `localStorage` values for
  extension setup, welcome/upgrade dialogs, vault onboarding, and the new-experience
  tour. The account ID comes from server verification. For an older manifest, run
  `./e2e/Server/server.sh verify` once to populate `userId`. Fixtures testing these
  flows can override `ConfigureOnboardingAsync` with `Task.CompletedTask` and prepare their scenario in shared setup.
- **Browser:** builds the selected Chrome, Edge, or Firefox extension, launches a fresh browser per test, installs
  the unpacked extension through WebDriver BiDi, and opens its popup. `ExtensionId`
  is available to derived tests. The test chooses `ServerUrl` in the client's
  self-hosted environment selector before logging in.
- **Desktop:** builds the native module and Electron bundles, launches the built
  Electron app with a fresh `BITWARDEN_APPDATA_DIR` and dynamic DevTools port per
  test, and attaches Dramaturge to the actual main window. Choose `ServerUrl` in
  the client's environment selector before logging in. Headed runs require a display;
  headless Linux runs use `xvfb-run` (install Xvfb and xauth).

Teardown closes each Web test's isolated browser, or the custom Browser/Electron
process. Web's namespace setup fixture closes its shared group at namespace teardown. The manually
started Web server and Docker backend remain running. Logs and fresh desktop
profiles are under ignored `e2e/TestResults/`.
Failures and startup timeouts point to the relevant log file.

```bash
dotnet test e2e/e2e.csproj
dotnet test e2e/e2e.csproj --filter FullyQualifiedName~e2e.Web.Tests
dotnet test e2e/e2e.csproj --filter TestCategory=Infrastructure
```

Each client directory has a `SetupFixture.cs` with editable browser and headless
defaults. Environment variables override those defaults independently:

| Client  | Browser override      | Headless override      | Defaults                             |
| ------- | --------------------- | ---------------------- | ------------------------------------ |
| Web     | `E2E_WEB_BROWSER`     | `E2E_WEB_HEADLESS`     | Chrome, headed                       |
| Browser | `E2E_BROWSER_BROWSER` | `E2E_BROWSER_HEADLESS` | Chrome, headless                     |
| Desktop | `E2E_DESKTOP_BROWSER` | `E2E_DESKTOP_HEADLESS` | Electron/Chromium (`chrome`), headed |

Browser values are `chrome`, `firefox`, `edge`, or `safari` for Web (subject to
Dramaturge/platform support), and `chrome`, `firefox`, or `edge` for extension tests.
Desktop attaches to Electron and accepts only `chrome`; selecting another browser
cannot change Electron's engine. Desktop headless mode uses a virtual display on Linux.
Headless values accept `true`/`false` or `1`/`0`.

For example, run headed Firefox Web tests with:

```bash
E2E_WEB_BROWSER=firefox E2E_WEB_HEADLESS=false \
dotnet test e2e/e2e.csproj --filter FullyQualifiedName~e2e.Web.Tests
```

`DRAMATURGE_BROWSER` and `DRAMATURGE_HEADED` remain global fallbacks when the
client-specific variable is absent. `DRAMATURGE_CHANNEL` selects the Web/extension
release channel. Set `E2E_REPO_ROOT` if the test assembly runs outside a repository checkout.
