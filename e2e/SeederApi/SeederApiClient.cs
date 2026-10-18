using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using e2e.Infrastructure.Http;

namespace e2e.SeederApi;

/// <summary>Typed access to the local server's seed, query, and play-scoped cleanup endpoints.</summary>
public sealed class SeederApiClient : IDisposable
{
    private readonly ApiHttpClient http;

    public SeederApiClient(SeederApiSettings settings, HttpMessageHandler? handler = null)
    {
        http = new ApiHttpClient(settings.SeederApiUrl, handler);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.SeederApiUsername}:{settings.SeederApiPassword}")));
    }

    public Task<SeedResponse<T>> SeedAsync<T>(string playId, string template, object arguments, CancellationToken cancellationToken = default) =>
        SendAsync<SeedResponse<T>>(HttpMethod.Post, "seed", playId, template, new { template, arguments }, cancellationToken);

    public Task<T> QueryAsync<T>(string playId, string template, object arguments, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Post, "query", playId, template, new { template, arguments }, cancellationToken);

    public async Task DeleteAsync(string playId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "seed/" + Uri.EscapeDataString(playId));
        await http.SendAsync(request, "Seeder API cleanup", cancellationToken);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, string playId, string template, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Play-Id", playId);
        return await http.SendJsonAsync<T>(request, $"Seeder API {path} {template}", cancellationToken);
    }

    public void Dispose() => http.Dispose();
}
