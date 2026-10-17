using Dramaturge.NUnit;
using e2e.Infrastructure;
using NUnit.Framework;

namespace e2e.Web;

[NonParallelizable]
public abstract class BaseTest : PageTest
{
    protected SeededEnvironment Server { get; private set; } = null!;
    protected SeededAccount Account => Server.Accounts[nameof(ClientKind.Web)];
    protected string ServerUrl => Server.ServerUrl;
    protected virtual bool SkipOnboarding => true;

    [OneTimeSetUp]
    public async Task PrepareClientAsync()
    {
        Server = await TestRuntime.Shared.PrepareAsync(ClientKind.Web);
        ArtifactsDirectory = Path.Combine(TestRuntime.Shared.Root, "e2e", "TestResults", "Dramaturge");
    }

    [SetUp]
    public async Task OpenWebAsync()
    {
        if (SkipOnboarding)
            await Page.AddInitScriptAsync(WebTestState.CreateInitScript(TestRuntime.Shared.WebUrl, Account));
        await Page.NavigateAsync(TestRuntime.Shared.WebUrl);
    }
}
