using Dramaturge;
using Dramaturge.Browsers;
using e2e.Infrastructure;
using e2e.SeederApi;
using NUnit.Framework;

namespace e2e;

[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract class BaseTest
{
    protected abstract ClientKind Client { get; }
    protected virtual string BrowserBuildTarget => "chrome";
    protected SeededEnvironment Server { get; private set; } = null!;
    protected SeederSession Seeds { get; private set; } = null!;
    protected SeededAccount Account => Seeds.Account;
    protected string ServerUrl => Server.ServerUrl;
    protected string RepositoryRoot => TestRuntime.Shared.Root;
    protected BrowserGroup Group { get; private set; } = null!;
    protected Page Page { get; private set; } = null!;

    public async Task PrepareClientAsync() => Server = await TestRuntime.Shared.PrepareAsync(Client, BrowserBuildTarget);

    [SetUp]
    public async Task StartClientAsync()
    {
        await PrepareClientAsync();
        Seeds = new SeederSession(new SeederApiClient(Server.SeederSettings));
        await Seeds.ProvisionAsync();
        await LaunchClientAsync();
    }

    private async Task LaunchClientAsync()
    {
        try
        {
            Group = await LaunchAsync();
            Page = await OpenPageAsync();
        }
        catch (Exception setupError)
        {
            try { await StopClientAsync(); }
            catch (Exception cleanupError) { throw new AggregateException("Client setup and cleanup failed.", setupError, cleanupError); }
            throw;
        }
    }

    protected virtual Task<BrowserGroup> LaunchAsync() => BrowserGroup.LaunchAsync(
        BrowserLauncher.ConfigureFromEnvironment().WithSessionCapability("acceptInsecureCerts", true));

    protected abstract Task<Page> OpenPageAsync();
    protected virtual ValueTask StopApplicationAsync() => ValueTask.CompletedTask;

    [TearDown]
    public async Task StopClientAsync()
    {
        try { if (Group is not null) await Group.DisposeAsync(); }
        finally
        {
            try { await StopApplicationAsync(); }
            finally
            {
                Group = null!; Page = null!;
                if (Seeds is not null) await Seeds.DisposeAsync();
            }
        }
    }
}
