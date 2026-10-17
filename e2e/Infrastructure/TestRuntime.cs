using System.Text.Json;

namespace e2e.Infrastructure;

public enum ClientKind { Browser, Desktop, Web }

public sealed record SeededAccount(string Email, string Password, string? UserId = null);
public sealed record SeededEnvironment(string ServerUrl, Dictionary<string, SeededAccount> Accounts);

internal sealed class TestRuntime
{
    public static TestRuntime Shared { get; } = new(FindRepository());
    private readonly SemaphoreSlim gate = new(1);
    private readonly Dictionary<ClientKind, string> built = [];
    private SeededEnvironment? server;

    internal TestRuntime(string root) => Root = root;
    public string Root { get; }
    public string WebUrl => $"https://localhost:{Environment.GetEnvironmentVariable("E2E_WEB_PORT") ?? "18080"}";
    public string Logs => Path.Combine(Root, "e2e", "TestResults", "startup");
    private string Manifest => Path.Combine(Root, "e2e", "Server", ".state", "test-environment.json");

    public async Task<SeededEnvironment> PrepareAsync(ClientKind client, string browserTarget = "chrome")
    {
        if (client == ClientKind.Web)
        {
            if (!File.Exists(Manifest))
                throw new InvalidOperationException($"Seeded environment manifest is missing: {Manifest}. Run ./e2e/Server/server.sh up before running Web tests.");
            return await ReadEnvironmentAsync();
        }

        await gate.WaitAsync();
        try
        {
            if (server is null)
            {
                // Verification checks all seeded logins and vaults, not merely an open port.
                if (!File.Exists(Manifest) || await RunAsync("server-verify", "bash",
                    ["e2e/Server/server.sh", "verify"], TimeSpan.FromMinutes(2)) != 0)
                    await RequireAsync("server-up", "bash", ["e2e/Server/server.sh", "up"]);

                server = await ReadEnvironmentAsync();
            }

            var target = client == ClientKind.Browser ? browserTarget : "default";
            if (!built.TryGetValue(client, out var previousTarget) || previousTarget != target)
            {
                if (!Directory.Exists(Path.Combine(Root, "node_modules")))
                    throw new InvalidOperationException("Client dependencies are missing. Run npm ci at the repository root.");

                string[] arguments = client == ClientKind.Browser
                    ? [$"e2e/{client}/build.sh", browserTarget] : [$"e2e/{client}/build.sh"];
                await RequireAsync($"{client.ToString().ToLowerInvariant()}-build", "bash", arguments);
                built[client] = target;
            }
            return server;
        }
        finally { gate.Release(); }
    }

    private async Task<SeededEnvironment> ReadEnvironmentAsync() =>
        JsonSerializer.Deserialize<SeededEnvironment>(await File.ReadAllTextAsync(Manifest),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException($"Invalid server manifest: {Manifest}");

    internal async Task<int> RunAsync(string name, string executable, string[] arguments, TimeSpan timeout)
    {
        await using var process = ManagedProcess.Start(Root, Path.Combine(Logs, $"{name}.log"), executable, arguments);
        return await process.WaitAsync(timeout);
    }

    private async Task RequireAsync(string name, string executable, string[] arguments)
    {
        if (await RunAsync(name, executable, arguments, TimeSpan.FromMinutes(60)) != 0)
            throw new InvalidOperationException($"{name} failed. See {Path.Combine(Logs, name + ".log")}");
    }

    private static string FindRepository()
    {
        var configured = Environment.GetEnvironmentVariable("E2E_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "e2e", "e2e.csproj"))) return dir.FullName;
        throw new DirectoryNotFoundException("Could not locate e2e/e2e.csproj. Set E2E_REPO_ROOT.");
    }
}
