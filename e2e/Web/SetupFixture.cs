using Dramaturge.Browsers;
using Dramaturge.NUnit;
using e2e.Infrastructure;
using NUnit.Framework;

namespace e2e.Web;

[SetUpFixture]
public sealed class SetupFixture : DramaturgeSetUpFixture
{
    internal static ClientLaunchSettings Settings { get; } =
        ClientLaunchSettings.Read("WEB", browser: BrowserKind.Chrome, headless: false);

    protected override BrowserLauncherBuilder ConfigureLauncher(BrowserLauncherBuilder builder) =>
        Settings.CreateLauncher();
}
