# Client builds for test runs

These Bash entry points use the npm workspace commands in the parent repository.
They can be invoked from any working directory. Install the repository's Node/npm
dependencies with `npm ci` first. Desktop also requires the native build
prerequisites used by `apps/desktop/desktop_native` (Rust and platform libraries).

From the repository root:

| Script | Result |
| --- | --- |
| `./e2e/Web/build-and-start.sh` | Builds and serves the licensed development web vault at `https://localhost:8080` by default. |
| `./e2e/Browser/build.sh [browser]` | Builds the licensed extension for the selected browser in `apps/browser/build` (default: Chrome). |
| `./e2e/Desktop/build.sh` | Builds the native module, then Electron main, renderer, and preload bundles in `apps/desktop/build`. |

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

The web script stays in the foreground and watches for changes. The test runner
should launch it as a managed process, wait for an HTTPS response before opening
the client, allow the development certificate, and terminate its process tree at
teardown. A listening port alone does not mean compilation has finished.

For manual use, the web client uses the existing `apps/web/config` settings,
including `local.json` overrides. When `E2E_SERVER_URL` is set, the script uses
`Web/webpack.config.cjs` to route requests to the [local e2e server](Server/README.md).
It sets the compiled client URLs to the web origin and routes API/Identity traffic
through the dev proxy, preserving the self-hosted path prefixes. No local config
files are overwritten. `E2E_WEB_URL` defaults to `https://localhost:18080` in this mode.

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
The shared `e2e.BaseTest` provides `Page`, `Group`, `ServerUrl`, and the respective
seeded `Account.Email` / `Account.Password`. Fixtures run sequentially because the
seeded accounts and build outputs are shared.

On the first fixture, the runtime verifies all three seeded server logins and vaults.
If that check fails or the server manifest is missing, it runs `Server/server.sh up`
and waits for pre-seeding to finish. Each client build runs once per test process.

- **Web:** starts the licensed webpack dev server on `https://localhost:18080`
  (`E2E_WEB_PORT` overrides the port). Waits for a successful compilation, the actual
  web page, and the backend proxy before opening an isolated browser per test.
- **Browser:** builds Chrome, launches a fresh Chrome for Testing per test, installs
  the unpacked extension through WebDriver BiDi, and opens its popup. `ExtensionId`
  is available to derived tests. The test chooses `ServerUrl` in the client's
  self-hosted environment selector before logging in.
- **Desktop:** builds the native module and Electron bundles, launches the built
  Electron app with a fresh `BITWARDEN_APPDATA_DIR` and dynamic DevTools port per
  test, and attaches Dramaturge to the actual main window. Choose `ServerUrl` in
  the client's environment selector before logging in. A desktop display (or Xvfb)
  is required.

Teardown closes each test's browser or Electron process. Assembly teardown stops
the web server started by the tests; the side-loaded Docker server remains running
for reuse. Logs and fresh desktop profiles are under ignored `e2e/TestResults/`.
Failures and startup timeouts point to the relevant log file.

```bash
dotnet test e2e/e2e.csproj
dotnet test e2e/e2e.csproj --filter FullyQualifiedName~e2e.Web.Tests
dotnet test e2e/e2e.csproj --filter TestCategory=Infrastructure
```

Web uses Dramaturge's `DRAMATURGE_BROWSER`, `DRAMATURGE_CHANNEL`, and
`DRAMATURGE_HEADED` settings; extension tests use Chrome for Testing, with
`DRAMATURGE_HEADED=1` enabling a visible window. Set `E2E_REPO_ROOT` if the test
assembly is run outside a repository checkout.
