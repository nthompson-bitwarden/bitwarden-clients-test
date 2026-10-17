using Dramaturge.NUnit;
using e2e.Infrastructure;
using NUnit.Framework;

namespace e2e.Web;

[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract class BaseTest : PageTest
{
    protected SeededEnvironment Server { get; private set; } = null!;
    protected SeededAccount Account => Server.Accounts[nameof(ClientKind.Web)];
    protected string ServerUrl => Server.ServerUrl;
    protected virtual Task ConfigureOnboardingAsync() =>
        Page.AddInitScriptAsync(WebTestState.CreateInitScript(TestRuntime.Shared.WebUrl, Account));

    public async Task PrepareClientAsync()
    {
        Server = await TestRuntime.Shared.PrepareAsync(ClientKind.Web);
        ArtifactsDirectory = Path.Combine(TestRuntime.Shared.Root, "e2e", "TestResults", "Dramaturge");
    }

    [SetUp]
    public async Task OpenWebAsync()
    {
        await PrepareClientAsync();
        await Page.SetViewportSizeAsync(1440, 1000);
        await ConfigureOnboardingAsync();
        await Page.NavigateAsync(TestRuntime.Shared.WebUrl);
    }
}
