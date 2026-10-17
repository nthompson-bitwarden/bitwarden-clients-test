using System.Text.Json;
using Dramaturge.Browsers;
using e2e.Infrastructure;
using NUnit.Framework;

namespace e2e.Browser;

[SetUpFixture]
public sealed class SetupFixture
{
    internal static ClientLaunchSettings Settings { get; private set; } = null!;
    internal static string FirefoxExtensionHost { get; } = Guid.NewGuid().ToString("D");
    internal static string BuildTarget => Settings.Browser.ToString().ToLowerInvariant();

    [OneTimeSetUp]
    public void Configure()
    {
        Settings = ClientLaunchSettings.Read("BROWSER", browser: BrowserKind.Chrome, headless: true);
        if (Settings.Browser is not (BrowserKind.Chrome or BrowserKind.Edge or BrowserKind.Firefox))
            throw new InvalidOperationException("Extension tests support Chrome, Edge, and Firefox.");
    }

    internal static BrowserLauncherBuilder CreateLauncher(string extensionPath)
    {
        var builder = Settings.CreateLauncher();
        if (Settings.Browser == BrowserKind.Firefox)
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(extensionPath, "manifest.json")));
            var id = manifest.RootElement.GetProperty("browser_specific_settings").GetProperty("gecko").GetProperty("id").GetString()!;
            return builder.WithBrowserOptions(new FirefoxLaunchOptions
            {
                Preferences =
                {
                    ["extensions.webextensions.uuids"] = JsonSerializer.Serialize(new Dictionary<string, string> { [id] = FirefoxExtensionHost }),
                },
            });
        }
        return builder.WithoutDefaultArguments("--disable-extensions")
            .WithArguments("--enable-unsafe-extension-debugging");
    }
}
