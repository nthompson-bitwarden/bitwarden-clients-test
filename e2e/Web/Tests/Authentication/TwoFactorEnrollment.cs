using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dramaturge;
using e2e.SeederApi;
using e2e.Infrastructure;

namespace e2e.Web.Tests.Authentication;

/// <summary>Enrolls test factors through the server's real authenticated management endpoints.</summary>
internal sealed class TwoFactorEnrollment(string serverUrl, SeederSession seeds) : IDisposable
{
    private readonly HttpClient http = new() { BaseAddress = new Uri(serverUrl), Timeout = TimeSpan.FromSeconds(30) };
    private string passwordHash = null!;
    private string? totpSecret;
    private long enrollmentStep;

    internal async Task AuthenticateAsync()
    {
        var account = seeds.Account;
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(account.Password),
            Encoding.UTF8.GetBytes(account.Email.Trim().ToLowerInvariant()), seeds.KdfIterations, HashAlgorithmName.SHA256, 32);
        passwordHash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(key, Encoding.UTF8.GetBytes(account.Password),
            1, HashAlgorithmName.SHA256, 32));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/identity/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password", ["username"] = account.Email, ["password"] = passwordHash,
                ["scope"] = "api offline_access", ["client_id"] = "web", ["deviceType"] = "9",
                ["deviceIdentifier"] = Guid.NewGuid().ToString(), ["deviceName"] = "E2E factor enrollment",
            }),
        };
        request.Headers.Add("Bitwarden-Client-Version", "2026.10.0");
        var result = await SendAsync(request);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.GetProperty("access_token").GetString());
        http.DefaultRequestHeaders.Add("X-Play-Id", seeds.PlayId);
    }

    internal async Task EnrollEmailAsync()
    {
        var details = await PostAsync("get-email", new { masterPasswordHash = passwordHash });
        var verification = details.GetProperty("userVerificationToken").GetString();
        await PostAsync("send-email", new { email = seeds.Account.Email, userVerificationToken = verification });
        var code = await seeds.GetEmailCodeAsync();
        await PutAsync("email", new { email = seeds.Account.Email, userVerificationToken = verification, token = code });
    }

    internal async Task EnrollTotpAsync()
    {
        var details = await PostAsync("get-authenticator", new { masterPasswordHash = passwordHash });
        totpSecret = details.GetProperty("authenticator").GetProperty("key").GetString()!;
        enrollmentStep = Totp.CurrentStep;
        await PutAsync("authenticator", new { key = totpSecret, token = Totp.Code(totpSecret, enrollmentStep),
            userVerificationToken = details.GetProperty("userVerificationToken").GetString() });
    }

    // A fresh account has used only its enrollment code. The server accepts the adjacent time step.
    internal string LoginTotpCode() => Totp.Code(totpSecret!, Math.Max(Totp.CurrentStep, enrollmentStep + 1));
    internal string InvalidTotpCode() => Totp.InvalidCode(totpSecret!);

    internal async Task EnrollWebAuthnAsync(Page page)
    {
        var details = await PostAsync("get-webauthn", new { masterPasswordHash = passwordHash });
        var verification = details.GetProperty("userVerificationToken").GetString();
        var challenge = await PostAsync("get-webauthn-challenge", new { userVerificationToken = verification });
        // Use the development vault origin, explicitly allowed by the local server configuration.
        var response = await page.EvaluateAsync<string>($$"""
            async () => {
                const options = {{challenge.GetProperty("options").GetRawText()}};
                const decode = value => Uint8Array.from(atob(value.replace(/-/g, '+').replace(/_/g, '/')), c => c.charCodeAt(0));
                const encode = value => btoa(String.fromCharCode(...new Uint8Array(value))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
                options.challenge = decode(options.challenge);
                options.user.id = decode(options.user.id);
                options.excludeCredentials = (options.excludeCredentials || []).map(c => ({...c, id: decode(c.id)}));
                const credential = await navigator.credentials.create({publicKey: options});
                return JSON.stringify({id: credential.id, rawId: encode(credential.rawId), type: credential.type,
                    response: {attestationObject: encode(credential.response.attestationObject),
                        clientDataJSON: encode(credential.response.clientDataJSON), transports: credential.response.getTransports()},
                    extensions: credential.getClientExtensionResults()});
            }
            """);
        await PutAsync("webauthn", new { id = 1, name = "E2E virtual passkey", userVerificationToken = verification,
            deviceResponse = JsonSerializer.Deserialize<JsonElement>(response) });
    }

    private Task<JsonElement> PostAsync(string path, object body) => RequestAsync(HttpMethod.Post, path, body);
    private Task<JsonElement> PutAsync(string path, object body) => RequestAsync(HttpMethod.Put, path, body);
    private Task<JsonElement> RequestAsync(HttpMethod method, string path, object body) =>
        SendAndDisposeAsync(new HttpRequestMessage(method, "/api/two-factor/" + path) { Content = JsonContent.Create(body) });

    private async Task<JsonElement> SendAndDisposeAsync(HttpRequestMessage request)
    {
        using (request) return await SendAsync(request);
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage request)
    {
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Factor setup {request.RequestUri!.AbsolutePath} failed with HTTP {(int)response.StatusCode}.");
        var content = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(content) ? default : JsonSerializer.Deserialize<JsonElement>(content);
    }

    public void Dispose() => http.Dispose();
}
