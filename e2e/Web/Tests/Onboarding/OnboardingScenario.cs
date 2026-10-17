using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Dramaturge;
using e2e.Infrastructure;

namespace e2e.Web.Tests.Onboarding;

internal enum OnboardingPrompt
{
    ExtensionSetup,
    Welcome,
    ExtensionReminder,
    Checklist,
    Premium,
    NewExperience,
    Walkthrough,
}

/// <summary>Controls prompt eligibility without changing the shared seeded account on the server.</summary>
internal sealed class OnboardingScenario(Page page, string serverUrl, SeededAccount account) : IDisposable
{
    private readonly HttpClient _api = new();

    internal async Task PrepareAsync(OnboardingPrompt prompt, bool redesignedVault = false)
    {
        // Keep the real encrypted vault and profile; only vary marketing eligibility in responses.
        await page.RouteAsync(new Regex(@"/api/(config|sync|accounts/profile)(?:\?.*)?$"), async route =>
        {
            var uri = new Uri(route.Request.Url);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(new Uri(serverUrl), uri.PathAndQuery));
            foreach (var header in route.Request.Headers.Where(h =>
                         h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
                request.Headers.TryAddWithoutValidation(header.Name, header.Value.Value);

            using var response = await _api.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                var json = JsonNode.Parse(body)!;
                if (uri.AbsolutePath.EndsWith("/config"))
                {
                    json["settings"] ??= new JsonObject();
                    json["settings"]!["suppressOnboardingInterstitials"] = false;
                    json["featureStates"] ??= new JsonObject();
                    json["featureStates"]!["vfo1-foundation"] = redesignedVault;
                    json["featureStates"]!["pm-29438-dialog-with-extension-prompt-account-age"] = 0;
                    json["featureStates"]!["pm-32180-premium-upsell-account-age"] = 0;
                }
                else
                {
                    var profile = uri.AbsolutePath.EndsWith("/sync") ? json["profile"]! : json;
                    profile["creationDate"] = prompt is OnboardingPrompt.NewExperience or OnboardingPrompt.Walkthrough
                        ? "2026-10-01T00:00:00.000Z" // Existing account, before the VFO1 GA cutoff.
                        : DateTimeOffset.UtcNow.AddDays(-10).ToString("O");
                }
                body = json.ToJsonString();
            }

            await route.FulfillAsync((ulong)response.StatusCode, body,
                new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        });

        // Seed once on the current login page. Reloads must retain what the user's actions saved.
        await page.EvaluateAsync(WebTestState.CreateInitScript(new Uri(page.Url).GetLeftPart(UriPartial.Authority), account));
        var prefix = $"user_{Guid.Parse(account.UserId!):D}_";
        string[] pendingKeys = prompt switch
        {
            OnboardingPrompt.ExtensionSetup => ["setupExtensionDismissed_setupExtensionDismissed"],
            OnboardingPrompt.Welcome => ["vaultWelcomeDialog_vaultWelcomeDialogAcknowledged"],
            OnboardingPrompt.ExtensionReminder => ["vaultWelcomeExtensionDialogDismissed_vaultWelcomeExtensionDialogDismissed"],
            OnboardingPrompt.Checklist => ["vaultOnboarding_tasks"],
            OnboardingPrompt.Premium => ["billingLocal_premiumModalDismissed"],
            OnboardingPrompt.NewExperience or OnboardingPrompt.Walkthrough => ["nudges_vaultNudgeDismissed"],
            _ => throw new ArgumentOutOfRangeException(nameof(prompt)),
        };
        await page.EvaluateAsync($$"""
            () => {
                const prefix = {{JsonSerializer.Serialize(prefix)}};
                for (const key of {{JsonSerializer.Serialize(pendingKeys)}}) localStorage.removeItem(prefix + key);
            }
            """);

        if (prompt == OnboardingPrompt.Walkthrough)
            await page.EvaluateAsync($$"""
                () => localStorage.setItem({{JsonSerializer.Serialize(prefix + "nudges_vaultNudgeDismissed")}},
                    JSON.stringify({ 'vfo1-new-experience': { hasBadgeDismissed: true, hasSpotlightDismissed: true } }))
                """);

        if (prompt is OnboardingPrompt.NewExperience or OnboardingPrompt.Walkthrough)
        {
            // The announcement expires six months after GA. Cap only Date.now so the nudge
            // remains testable after that window, without moving the browser's clock forward.
            await page.AddInitScriptAsync("() => { const now = Date.now.bind(Date); Date.now = () => Math.min(now(), Date.parse('2027-01-01T00:00:00Z')); }");
        }
        await page.ReloadAsync();
    }

    public void Dispose() => _api.Dispose();
}
