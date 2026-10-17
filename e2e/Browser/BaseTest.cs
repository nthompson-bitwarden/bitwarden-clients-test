using System.Text.Json;
using Dramaturge;
using Dramaturge.Browsers;
using e2e.Infrastructure;
using WebDriverBiDi.WebExtension;

namespace e2e.Browser;

public abstract class BaseTest : e2e.BaseTest
{
    protected override ClientKind Client => ClientKind.Browser;
    protected string ExtensionId { get; private set; } = null!;

    protected override Task<BrowserGroup> LaunchAsync() => BrowserGroup.LaunchAsync(
        BrowserLauncher.Configure(BrowserKind.Chrome)
            .WithHeadlessOption(Environment.GetEnvironmentVariable("DRAMATURGE_HEADED") is not ("1" or "true"))
            .WithoutDefaultArguments("--disable-extensions")
            .WithArguments("--enable-unsafe-extension-debugging"));

    protected override async Task<Page> OpenPageAsync()
    {
        var path = Path.Combine(RepositoryRoot, "apps", "browser", "build");
        var installed = await Group.Driver.WebExtension.InstallAsync(new InstallCommandParameters(new ExtensionPath(path)));
        ExtensionId = installed.ExtensionId;
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(path, "manifest.json")));
        var popup = manifest.RootElement.GetProperty("action").GetProperty("default_popup").GetString();
        var page = await Group.DefaultBrowser.NewPageAsync();
        await page.NavigateAsync($"chrome-extension://{ExtensionId}/{popup}");
        return page;
    }
}
