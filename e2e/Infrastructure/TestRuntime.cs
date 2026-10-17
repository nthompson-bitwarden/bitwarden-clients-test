using System.Text.Json;

namespace e2e.Infrastructure;

public enum ClientKind { Browser, Desktop, Web }

public sealed record SeededAccount(string Email, string Password);
public sealed record SeededEnvironment(string ServerUrl, Dictionary<string, SeededAccount> Accounts);

internal sealed class TestRuntime : IAsyncDisposable
{
    public static TestRuntime Shared { get; } = new(FindRepository());
    private readonly SemaphoreSlim gate = new(1);
    private readonly HashSet<ClientKind> built = [];
    private SeededEnvironment? server;
    private ManagedProcess? web;

    internal TestRuntime(string root) => Root = root;
    public string Root { get; }
    public string WebUrl => $"https://localhost:{Environment.GetEnvironmentVariable("E2E_WEB_PORT") ?? "18080"}";
    public string Logs => Path.Combine(Root, "e2e", "TestResults", "startup");
    private string Manifest => Path.Combine(Root, "e2e", "Server", ".state", "test-environment.json");

    public async Task<SeededEnvironment> PrepareAsync(ClientKind client)
    {
        await gate.WaitAsync();
        try
        {
            if (server is null)
            {
                // Verification checks all seeded logins and vaults, not merely an open port.
                if (!File.Exists(Manifest) || await RunAsync("server-verify", "bash",
                    ["e2e/Server/server.sh", "verify"], TimeSpan.FromMinutes(2)) != 0)
                    await RequireAsync("server-up", "bash", ["e2e/Server/server.sh", "up"]);

                server = JsonSerializer.Deserialize<SeededEnvironment>(await File.ReadAllTextAsync(Manifest),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException($"Invalid server manifest: {Manifest}");
            }

            if (!built.Contains(client))
            {
                if (!Directory.Exists(Path.Combine(Root, "node_modules")))
                    throw new InvalidOperationException("Client dependencies are missing. Run npm ci at the repository root.");

                if (client == ClientKind.Web)
                {
                    var statusPath = Path.Combine(Logs, "web-build.json");
                    File.Delete(statusPath);
                    web = ManagedProcess.Start(Root, Path.Combine(Logs, "web.log"), "bash",
                        ["e2e/Web/build-and-start.sh", "--port", new Uri(WebUrl).Port.ToString()],
                        new Dictionary<string, string> { ["E2E_SERVER_URL"] = server.ServerUrl,
                            ["E2E_WEB_URL"] = WebUrl, ["E2E_WEB_BUILD_STATUS"] = statusPath });
                    try { await WaitForWebAsync(web, statusPath); }
                    catch { await web.DisposeAsync(); web = null; throw; }
                }
                else
                    await RequireAsync($"{client.ToString().ToLowerInvariant()}-build", "bash", [$"e2e/{client}/build.sh"]);
                built.Add(client);
            }
            if (client == ClientKind.Web && web!.HasExited)
                throw new InvalidOperationException($"Web dev server exited. See {web.LogPath}");
            return server;
        }
        finally { gate.Release(); }
    }

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

    private async Task WaitForWebAsync(ManagedProcess process, string statusPath)
    {
        using var handler = new HttpClientHandler
        {
            // Only the local web dev server uses the repository's development certificate.
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMinutes(15);
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited) throw new InvalidOperationException($"Web build exited. See {process.LogPath}");
            try
            {
                if (!File.Exists(statusPath)) { await Task.Delay(500); continue; }
                using var status = JsonDocument.Parse(await File.ReadAllTextAsync(statusPath));
                if (status.RootElement.GetProperty("hasErrors").GetBoolean())
                    throw new InvalidOperationException($"Web compilation failed. See {process.LogPath}");
                using var response = await http.GetAsync(WebUrl);
                // Require successful compilation, the actual web page, and a working backend proxy.
                if (response.IsSuccessStatusCode && (await response.Content.ReadAsStringAsync()).Contains("<app-root"))
                {
                    using var api = await http.GetAsync(WebUrl + "/api/config");
                    if (api.IsSuccessStatusCode) return;
                }
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException) { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Web client did not become ready. See {process.LogPath}");
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

    public async ValueTask DisposeAsync()
    {
        if (web is not null) { await web.DisposeAsync(); web = null; }
        // The side-loaded server is deliberately retained for subsequent test runs.
    }
}
