using System.Text.Json;
using NUnit.Framework;

namespace e2e.Infrastructure.Tests;

[TestFixture]
[Category("Infrastructure")]
public sealed class TestRuntimeTests
{
    private string root = null!;

    [SetUp]
    public void CreateRepository()
    {
        root = Path.Combine(Path.GetTempPath(), "e2e infrastructure " + Guid.NewGuid().ToString("N"));
        foreach (var directory in new[] { "node_modules", "e2e/Server/.state", "e2e/Browser", "e2e/Desktop" })
            Directory.CreateDirectory(Path.Combine(root, directory));
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new
        {
            serverUrl = "http://localhost:18090",
            seederApiUrl = "http://localhost:18092",
            seederApiUsername = "e2e",
            seederApiPassword = "test-only-password",
        }));
        File.WriteAllText(Path.Combine(root, "e2e/Server/server.sh"), """
            set -eu
            echo "$1" >> server-calls
            case "$1" in
              verify) test -f ready ;;
              up) touch ready; cp manifest.json e2e/Server/.state/test-environment.json ;;
            esac
            """);
        foreach (var client in new[] { "Browser", "Desktop" })
            File.WriteAllText(Path.Combine(root, $"e2e/{client}/build.sh"), $"echo {client} >> build-calls\n");
    }

    [TearDown]
    public void DeleteRepository() => Directory.Delete(root, recursive: true);

    [Test]
    public async Task WebOnlyReadsExistingManifestWithoutDependenciesOrStartup()
    {
        File.Copy(Path.Combine(root, "manifest.json"), Path.Combine(root, "e2e/Server/.state/test-environment.json"));
        Directory.Delete(Path.Combine(root, "node_modules"));
        var runtime = new TestRuntime(root);

        var environment = await runtime.PrepareAsync(ClientKind.Web);

        Assert.That(environment.SeederApiUrl, Is.EqualTo("http://localhost:18092"));
        Assert.That(File.Exists(Path.Combine(root, "server-calls")), Is.False);
        Assert.That(File.Exists(Path.Combine(root, "build-calls")), Is.False);
        Assert.That(Directory.Exists(runtime.Logs), Is.False);
    }

    [Test]
    public void WebRequiresManuallySeededEnvironment()
    {
        var runtime = new TestRuntime(root);

        var error = Assert.ThrowsAsync<InvalidOperationException>(() => runtime.PrepareAsync(ClientKind.Web));

        Assert.That(error!.Message, Does.Contain("./e2e/Server/server.sh up"));
        Assert.That(File.Exists(Path.Combine(root, "server-calls")), Is.False);
    }

    [Test]
    public void LegacyManifestReportsSeederApiUpgradeWithoutStartingServices()
    {
        //Arrange
        File.WriteAllText(Path.Combine(root, "e2e/Server/.state/test-environment.json"),
            "{\"serverUrl\":\"http://localhost:18090\",\"accounts\":{}}");
        var runtime = new TestRuntime(root);

        //Act
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => runtime.PrepareAsync(ClientKind.Web));

        //Assert
        Assert.That(error!.Message, Does.Contain("Seeder API").And.Contain("server.sh up"));
        Assert.That(File.Exists(Path.Combine(root, "server-calls")), Is.False);
    }

    [TestCase(ClientKind.Browser)]
    [TestCase(ClientKind.Desktop)]
    public async Task WebPreparationDoesNotBypassManagedBackendVerification(ClientKind client)
    {
        File.Copy(Path.Combine(root, "manifest.json"), Path.Combine(root, "e2e/Server/.state/test-environment.json"));
        var runtime = new TestRuntime(root);

        await runtime.PrepareAsync(ClientKind.Web);
        await runtime.PrepareAsync(client);

        Assert.That(File.ReadAllLines(Path.Combine(root, "server-calls")), Is.EqualTo(new[] { "verify", "up" }));
        Assert.That(File.ReadAllLines(Path.Combine(root, "build-calls")), Is.EqualTo(new[] { client.ToString() }));
    }

    [Test]
    public async Task ConcurrentFixturesStartServerAndBuildEachClientOnce()
    {
        var runtime = new TestRuntime(root);
        await Task.WhenAll(runtime.PrepareAsync(ClientKind.Browser), runtime.PrepareAsync(ClientKind.Desktop),
            runtime.PrepareAsync(ClientKind.Browser));
        Assert.That(File.ReadAllLines(Path.Combine(root, "server-calls")), Is.EqualTo(new[] { "up" }));
        Assert.That(File.ReadAllLines(Path.Combine(root, "build-calls")), Is.EquivalentTo(new[] { "Browser", "Desktop" }));
    }

    [Test]
    public async Task ExtensionBuildUsesSelectedBrowserAndRebuildsWhenTargetChanges()
    {
        //Arrange
        File.WriteAllText(Path.Combine(root, "e2e/Browser/build.sh"), "echo \"$1\" >> build-calls\n");
        var runtime = new TestRuntime(root);

        //Act
        await runtime.PrepareAsync(ClientKind.Browser, "firefox");
        await runtime.PrepareAsync(ClientKind.Browser, "firefox");
        await runtime.PrepareAsync(ClientKind.Browser, "edge");

        //Assert
        Assert.That(File.ReadAllLines(Path.Combine(root, "build-calls")), Is.EqualTo(new[] { "firefox", "edge" }));
    }

    [Test]
    public async Task HealthySeededServerIsReused()
    {
        File.Copy(Path.Combine(root, "manifest.json"), Path.Combine(root, "e2e/Server/.state/test-environment.json"));
        File.WriteAllText(Path.Combine(root, "ready"), "");
        var runtime = new TestRuntime(root);
        var environment = await runtime.PrepareAsync(ClientKind.Browser);
        Assert.That(File.ReadAllLines(Path.Combine(root, "server-calls")), Is.EqualTo(new[] { "verify" }));
        Assert.That(environment.SeederApiUsername, Is.EqualTo("e2e"));
    }

    [Test]
    public async Task UnhealthyServerIsStartedBeforeBuildingClient()
    {
        File.Copy(Path.Combine(root, "manifest.json"), Path.Combine(root, "e2e/Server/.state/test-environment.json"));
        var runtime = new TestRuntime(root);
        await runtime.PrepareAsync(ClientKind.Desktop);
        Assert.That(File.ReadAllLines(Path.Combine(root, "server-calls")), Is.EqualTo(new[] { "verify", "up" }));
    }

    [Test]
    public async Task FailedBuildIsReportedAndCanBeRetried()
    {
        var script = Path.Combine(root, "e2e/Browser/build.sh");
        File.WriteAllText(script, "echo compiler-failed >&2; exit 7\n");
        var runtime = new TestRuntime(root);
        Assert.ThrowsAsync<InvalidOperationException>(() => runtime.PrepareAsync(ClientKind.Browser));
        Assert.That(File.ReadAllText(Path.Combine(runtime.Logs, "browser-build.log")), Does.Contain("compiler-failed"));
        File.WriteAllText(script, "echo Browser >> build-calls\n");
        await runtime.PrepareAsync(ClientKind.Browser);
        Assert.That(File.ReadAllLines(Path.Combine(root, "build-calls")), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task ProcessDrainsBothPipesAndReportsExitCode()
    {
        await using var process = ManagedProcess.Start(root, Path.Combine(root, "process.log"), "bash",
            ["-c", "for ((i=0;i<5000;i++)); do echo out; echo err >&2; done; exit 13"]);
        Assert.That(await process.WaitAsync(TimeSpan.FromSeconds(10)), Is.EqualTo(13));
        Assert.That(File.ReadAllLines(process.LogPath), Has.Length.EqualTo(10000));
    }

    [Test]
    public async Task ProcessTimeoutStopsOwnedProcess()
    {
        var process = ManagedProcess.Start(root, Path.Combine(root, "timeout.log"), "bash", ["-c", "sleep 120"]);
        try
        {
            Assert.ThrowsAsync<TimeoutException>(() => process.WaitAsync(TimeSpan.FromMilliseconds(50)));
        }
        finally { await process.DisposeAsync(); }
    }
}
