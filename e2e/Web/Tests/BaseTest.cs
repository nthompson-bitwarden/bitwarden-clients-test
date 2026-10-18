using Dramaturge.NUnit;
using e2e.Infrastructure;
using e2e.SeederApi;
using NUnit.Framework;

namespace e2e.Web;

[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract class BaseTest : PageTest
{
    protected SeededEnvironment Server { get; private set; } = null!;
    protected SeederSession Seeds { get; private set; } = null!;
    protected SeededAccount Account => Seeds.Account;
    protected string ServerUrl => Server.ServerUrl;
    protected virtual Task ConfigureOnboardingAsync() =>
        Page.AddInitScriptAsync(WebTestState.CreateInitScript(TestRuntime.Shared.WebUrl, Account));

    public async Task PrepareClientAsync()
    {
        Server = await TestRuntime.Shared.PrepareAsync(ClientKind.Web);
        ArtifactsDirectory = Path.Combine(TestRuntime.Shared.Root, "e2e", "TestResults", "Dramaturge");
    }

    [TearDown]
    public async Task DeleteSeededDataAsync()
    {
        if (Seeds is not null) await Seeds.DisposeAsync();
    }

    private async Task OpenSeededPageAsync()
    {
        try
        {
            await Page.SetViewportSizeAsync(1440, 1000);
            await ConfigureOnboardingAsync();
            await Page.NavigateAsync(TestRuntime.Shared.WebUrl);
        }
        catch (Exception setupError)
        {
            try { await Seeds.DisposeAsync(); }
            catch (Exception cleanupError) { throw new AggregateException("Web setup and play cleanup failed.", setupError, cleanupError); }
            throw;
        }
    }

    [SetUp]
    public async Task OpenWebAsync()
    {
        await PrepareClientAsync();
        Seeds = new SeederSession(new SeederApiClient(Server.SeederSettings));
        await Seeds.ProvisionAsync();
        await OpenSeededPageAsync();
    }
}
