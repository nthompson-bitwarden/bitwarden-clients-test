using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using e2e.Infrastructure.Http;
using e2e.SeederApi;

namespace e2e.ServerApi;

/// <summary>Per-test authenticated access to the server's internal API and identity service.</summary>
internal sealed class ServerApiClient(string serverUrl, HttpMessageHandler? handler = null) : IDisposable
{
    private readonly ApiHttpClient http = new(serverUrl, handler);
    internal string MasterPasswordHash { get; private set; } = null!;

    internal async Task AuthenticateAsync(SeededAccount account, int kdfIterations, string playId,
        CancellationToken cancellationToken = default)
    {
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(account.Password),
            Encoding.UTF8.GetBytes(account.Email.Trim().ToLowerInvariant()), kdfIterations, HashAlgorithmName.SHA256, 32);
        MasterPasswordHash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(key, Encoding.UTF8.GetBytes(account.Password),
            1, HashAlgorithmName.SHA256, 32));
        using var request = new HttpRequestMessage(HttpMethod.Post, "identity/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password", ["username"] = account.Email, ["password"] = MasterPasswordHash,
                ["scope"] = "api offline_access", ["client_id"] = "web", ["deviceType"] = "9",
                ["deviceIdentifier"] = Guid.NewGuid().ToString(), ["deviceName"] = "E2E factor enrollment",
            }),
        };
        request.Headers.Add("Bitwarden-Client-Version", "2026.10.0");
        // A new login must not inherit the previous session's headers.
        http.DefaultRequestHeaders.Authorization = null;
        http.DefaultRequestHeaders.Remove("X-Play-Id");
        var result = await http.SendJsonAsync<JsonElement>(request, "Server API authentication", cancellationToken);
        if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("access_token", out var token) ||
            token.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.GetString()))
            throw new InvalidOperationException("Server API authentication returned no access token.");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetString());
        http.DefaultRequestHeaders.Add("X-Play-Id", playId);
    }

    internal async Task<JsonElement> SendAsync(HttpMethod method, string path, object body,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, "api/" + path.TrimStart('/')) { Content = JsonContent.Create(body) };
        return await http.SendJsonAsync<JsonElement>(request, "Server API request", cancellationToken, allowEmptyResponse: true);
    }

    public void Dispose() => http.Dispose();
}
