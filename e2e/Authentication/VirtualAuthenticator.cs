using System.Text.Json;
using Dramaturge;
using WebDriverBiDi;

namespace e2e.Authentication;

/// <summary>A Chromium virtual authenticator scoped to the current test's browsing target.</summary>
internal sealed class VirtualAuthenticator(Page page) : IAsyncDisposable
{
    private string? session;
    private string? authenticatorId;

    public async Task InitializeAsync()
    {
        var response = await page.Browser.Group.Driver.ExecuteCommandAsync(new CdpCommand("goog:cdp.getSession",
            new { context = page.Id }));
        session = JsonSerializer.SerializeToElement(response.AdditionalData).GetProperty("session").GetString();
        await SendAsync("WebAuthn.enable", new { enableUI = false });
        var added = await SendAsync("WebAuthn.addVirtualAuthenticator", new
        {
            options = new { protocol = "ctap2", transport = "internal", hasResidentKey = true,
                hasUserVerification = true, isUserVerified = true, automaticPresenceSimulation = true },
        });
        authenticatorId = added.GetProperty("authenticatorId").GetString();
    }

    public async Task<long> GetSignatureCountAsync()
    {
        var result = await SendAsync("WebAuthn.getCredentials", new { authenticatorId });
        return result.GetProperty("credentials").EnumerateArray().Sum(credential => credential.GetProperty("signCount").GetInt64());
    }

    private async Task<JsonElement> SendAsync(string method, object parameters)
    {
        var result = await page.Browser.Group.Driver.ExecuteCommandAsync(new CdpCommand("goog:cdp.sendCommand",
            new { method, @params = parameters, session }));
        return JsonSerializer.SerializeToElement(result.AdditionalData).GetProperty("result").Clone();
    }

    public async ValueTask DisposeAsync()
    {
        if (authenticatorId is not null)
            await SendAsync("WebAuthn.removeVirtualAuthenticator", new { authenticatorId });
        if (session is not null) await SendAsync("WebAuthn.disable", new { });
    }

    private sealed class CdpCommand : CommandParameters<CommandResult>
    {
        private readonly string method;
        public override string MethodName => method;

        public CdpCommand(string method, object parameters)
        {
            this.method = method;
            foreach (var property in JsonSerializer.SerializeToElement(parameters).EnumerateObject())
                AdditionalData[property.Name] = property.Value.Clone();
        }
    }
}
