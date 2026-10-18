using System.Net.Http.Headers;
using System.Text.Json;

namespace e2e.Infrastructure.Http;

/// <summary>Shared transport for test API clients. Each instance owns its HTTP client and handler.</summary>
internal sealed class ApiHttpClient : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient http;

    internal ApiHttpClient(string baseUrl, HttpMessageHandler? handler = null)
    {
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(30);
    }

    internal HttpRequestHeaders DefaultRequestHeaders => http.DefaultRequestHeaders;

    // The caller owns the request and returned response. Raw forwarding preserves unsuccessful responses.
    internal Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) =>
        http.SendAsync(request, cancellationToken);

    internal async Task SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(request, cancellationToken);
        EnsureSuccess(response, operation);
    }

    internal async Task<T> SendJsonAsync<T>(HttpRequestMessage request, string operation,
        CancellationToken cancellationToken = default, bool allowEmptyResponse = false)
    {
        using var response = await SendRawAsync(request, cancellationToken);
        EnsureSuccess(response, operation);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            if (allowEmptyResponse) return default!;
            throw new InvalidOperationException($"{operation} returned an empty response.");
        }
        try
        {
            return JsonSerializer.Deserialize<T>(content, Json)
                ?? throw new InvalidOperationException($"{operation} returned an empty response.");
        }
        catch (JsonException)
        {
            // API bodies and JSON exception details may contain credentials or decrypted vault keys.
            throw new InvalidOperationException($"{operation} returned an invalid response.");
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{operation} failed with HTTP {(int)response.StatusCode}.");
    }

    public void Dispose() => http.Dispose();
}
