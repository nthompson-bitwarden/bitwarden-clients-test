using Dramaturge;
using Dramaturge.Browsers;
using e2e.Infrastructure;
using NUnit.Framework;

namespace e2e;

[NonParallelizable]
public abstract class BaseTest
{
    protected abstract ClientKind Client { get; }
    protected virtual string BrowserBuildTarget => "chrome";
    protected SeededEnvironment Server { get; private set; } = null!;
    protected SeededAccount Account => Server.Accounts[Client.ToString()];
    protected string ServerUrl => Server.ServerUrl;
    protected string RepositoryRoot => TestRuntime.Shared.Root;
    protected BrowserGroup Group { get; private set; } = null!;
    protected Page Page { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task PrepareClientAsync() => Server = await TestRuntime.Shared.PrepareAsync(Client, BrowserBuildTarget);

    [SetUp]
    public async Task StartClientAsync()
    {
        try
        {
            Group = await LaunchAsync();
            Page = await OpenPageAsync();
        }
        catch
        {
            await StopClientAsync();
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
        finally { await StopApplicationAsync(); Group = null!; Page = null!; }
    }
}
