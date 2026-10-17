using Dramaturge.Browsers;
using NUnit.Framework;

namespace e2e.Infrastructure.Tests;

[Category("Infrastructure")]
public sealed class ClientLaunchSettingsTests
{
    [Test]
    public void ClientOverridesGlobalSettingsWithoutAffectingOtherClients()
    {
        //Arrange
        Dictionary<string, string> environment = new()
        {
            ["DRAMATURGE_BROWSER"] = "chrome",
            ["DRAMATURGE_HEADED"] = "1",
            ["E2E_WEB_BROWSER"] = "firefox",
            ["E2E_WEB_HEADLESS"] = "true",
        };

        //Act
        var web = ClientLaunchSettings.Read("WEB", BrowserKind.Chrome, true, key => environment.GetValueOrDefault(key));
        var extension = ClientLaunchSettings.Read("BROWSER", BrowserKind.Chrome, true, key => environment.GetValueOrDefault(key));

        //Assert
        Assert.That(web, Is.EqualTo(new ClientLaunchSettings(BrowserKind.Firefox, true)));
        Assert.That(extension, Is.EqualTo(new ClientLaunchSettings(BrowserKind.Chrome, false)));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void MissingEnvironmentPreservesFixtureDefaults(bool headless)
    {
        //Arrange
        var browser = BrowserKind.Edge;

        //Act
        var settings = ClientLaunchSettings.Read("WEB", browser, headless, _ => null);

        //Assert
        Assert.That(settings, Is.EqualTo(new ClientLaunchSettings(browser, headless)));
    }

    [TestCase("E2E_WEB_BROWSER", "unknown")]
    [TestCase("E2E_WEB_BROWSER", "99")]
    [TestCase("E2E_WEB_HEADLESS", "maybe")]
    public void InvalidSettingsFailClearly(string key, string value)
    {
        //Arrange
        string? EnvironmentValue(string name) => name == key ? value : null;

        //Act
        var error = Assert.Throws<InvalidOperationException>(() =>
            ClientLaunchSettings.Read("WEB", BrowserKind.Chrome, true, EnvironmentValue));

        //Assert
        Assert.That(error!.Message, Does.Contain(value));
    }
}
