using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace e2e.SeederApi;

/// <summary>Typed access to the local server's seed, query, and play-scoped cleanup endpoints.</summary>
public sealed class SeederApiClient : IDisposable
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient http;

    public SeederApiClient(SeederApiSettings settings, HttpMessageHandler? handler = null)
    {
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.BaseAddress = new Uri(settings.SeederApiUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(30);
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
        using var response = await http.SendAsync(request, cancellationToken);
        EnsureSuccess(response, $"cleanup play {playId}");
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, string playId, string template, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Play-Id", playId);
        using var response = await http.SendAsync(request, cancellationToken);
        EnsureSuccess(response, $"{path} {template}");
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
                ?? throw new InvalidOperationException($"Seeder API {path} {template} returned an empty response.");
        }
        catch (JsonException)
        {
            // Response bodies may contain credentials or decrypted vault keys.
            throw new InvalidOperationException($"Seeder API {path} {template} returned an invalid response.");
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Seeder API {operation} failed with HTTP {(int)response.StatusCode}. Run ./e2e/Server/server.sh up and check the Seeder API logs.");
    }

    public void Dispose() => http.Dispose();
}
