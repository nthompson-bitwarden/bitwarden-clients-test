using System.Text.Json;
using e2e.SeederApi;

namespace e2e.Web;

internal static class WebTestState
{
    internal static string CreateInitScript(string webUrl, SeededAccount account)
    {
        if (!Guid.TryParse(account.UserId, out var userId))
            throw new InvalidOperationException(
                "Seeder API returned a missing or invalid account userId.");

        // Match UserKeyDefinition.buildKey and the web disk-local StateDefinitions.
        // Keep these aligned with the corresponding app services when their state changes.
        var prefix = $"user_{userId:D}_";
        Dictionary<string, object> state = new()
        {
            [prefix + "setupExtensionDismissed_setupExtensionDismissed"] = true,
            [prefix + "vaultWelcomeDialog_vaultWelcomeDialogAcknowledged"] = true,
            [prefix + "vaultWelcomeExtensionDialogDismissed_vaultWelcomeExtensionDialogDismissed"] = true,
            [prefix + "billingLocal_premiumModalDismissed"] = true,
            [prefix + "vaultOnboarding_tasks"] = new { createAccount = true, importData = true, installExtension = true },
            [prefix + "nudges_vaultNudgeDismissed"] = new Dictionary<string, object>
            {
                ["vfo1-new-experience"] = new { hasBadgeDismissed = true, hasSpotlightDismissed = true },
                ["vfo1-walkthrough"] = new { hasBadgeDismissed = true, hasSpotlightDismissed = true },
            },
        };
        var origin = new Uri(webUrl).GetLeftPart(UriPartial.Authority);
        return $$"""
            () => {
                if (window.location.origin !== {{JsonSerializer.Serialize(origin)}}) return;
                const state = {{JsonSerializer.Serialize(state)}};
                for (const [key, value] of Object.entries(state)) {
                    window.localStorage.setItem(key, JSON.stringify(value));
                }
            }
            """;
    }
}
