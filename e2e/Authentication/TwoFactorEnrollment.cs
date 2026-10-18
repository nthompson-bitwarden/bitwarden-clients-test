using System.Text.Json;
using Dramaturge;
using e2e.Infrastructure;
using e2e.SeederApi;
using e2e.ServerApi;

namespace e2e.Authentication;

/// <summary>Enrolls test factors through the server's real authenticated management endpoints.</summary>
internal sealed class TwoFactorEnrollment(string serverUrl, SeederSession seeds) : IDisposable
{
    private readonly ServerApiClient api = new(serverUrl);
    private string? totpSecret;
    private long enrollmentStep;

    internal Task AuthenticateAsync() => api.AuthenticateAsync(seeds.Account, seeds.KdfIterations, seeds.PlayId);

    internal async Task EnrollEmailAsync()
    {
        var details = await PostAsync("get-email", new { masterPasswordHash = api.MasterPasswordHash });
        var verification = details.GetProperty("userVerificationToken").GetString();
        await PostAsync("send-email", new { email = seeds.Account.Email, userVerificationToken = verification });
        var code = await seeds.GetEmailCodeAsync();
        await PutAsync("email", new { email = seeds.Account.Email, userVerificationToken = verification, token = code });
    }

    internal async Task EnrollTotpAsync()
    {
        var details = await PostAsync("get-authenticator", new { masterPasswordHash = api.MasterPasswordHash });
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
        var details = await PostAsync("get-webauthn", new { masterPasswordHash = api.MasterPasswordHash });
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

    private Task<JsonElement> PostAsync(string path, object body) => api.SendAsync(HttpMethod.Post, "two-factor/" + path, body);
    private Task<JsonElement> PutAsync(string path, object body) => api.SendAsync(HttpMethod.Put, "two-factor/" + path, body);

    public void Dispose() => api.Dispose();
}
