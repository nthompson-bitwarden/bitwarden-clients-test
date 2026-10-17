using System.Text.Json;
using Dramaturge;
using Dramaturge.Browsers;
using e2e.Infrastructure;
using WebDriverBiDi.WebExtension;

namespace e2e.Browser;

public abstract class BaseTest : e2e.BaseTest
{
    protected override ClientKind Client => ClientKind.Browser;
    protected override string BrowserBuildTarget => SetupFixture.BuildTarget;
    protected string ExtensionId { get; private set; } = null!;
    private string ExtensionPath => Path.Combine(RepositoryRoot, "apps", "browser", "build");

    protected override Task<BrowserGroup> LaunchAsync() => BrowserGroup.LaunchAsync(
        SetupFixture.CreateLauncher(ExtensionPath));

    protected override async Task<Page> OpenPageAsync()
    {
        var path = ExtensionPath;
        var installed = await Group.Driver.WebExtension.InstallAsync(new InstallCommandParameters(new ExtensionPath(path)));
        ExtensionId = installed.ExtensionId;
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(path, "manifest.json")));
        var action = manifest.RootElement.TryGetProperty("action", out var value)
            ? value : manifest.RootElement.GetProperty("browser_action");
        var popup = action.GetProperty("default_popup").GetString();
        var page = await Group.DefaultBrowser.NewPageAsync();
        var origin = SetupFixture.Settings.Browser == BrowserKind.Firefox
            ? $"moz-extension://{SetupFixture.FirefoxExtensionHost}"
            : $"chrome-extension://{ExtensionId}";
        await page.NavigateAsync($"{origin}/{popup}");
        return page;
    }
}
