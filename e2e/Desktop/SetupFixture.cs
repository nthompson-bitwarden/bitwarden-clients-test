using Dramaturge.Browsers;
using e2e.Infrastructure;
using NUnit.Framework;

namespace e2e.Desktop;

[SetUpFixture]
public sealed class SetupFixture
{
    internal static ClientLaunchSettings Settings { get; private set; } = null!;

    [OneTimeSetUp]
    public void Configure()
    {
        Settings = ClientLaunchSettings.Read("DESKTOP", browser: BrowserKind.Chrome, headless: false);
        if (Settings.Browser != BrowserKind.Chrome)
            throw new InvalidOperationException("Desktop tests attach to Electron's Chromium engine. Set E2E_DESKTOP_BROWSER=chrome.");
        if (Settings.Headless && !OperatingSystem.IsLinux())
            throw new InvalidOperationException("Headless Desktop tests require Linux with xvfb-run installed.");
    }
}
