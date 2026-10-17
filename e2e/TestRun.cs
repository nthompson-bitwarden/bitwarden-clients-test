using e2e.Infrastructure;
using NUnit.Framework;

// Global scope covers all three client namespaces, including filtered runs.
[SetUpFixture]
public sealed class TestRun
{
    [OneTimeTearDown]
    public async Task StopOwnedServicesAsync() => await TestRuntime.Shared.DisposeAsync();
}
