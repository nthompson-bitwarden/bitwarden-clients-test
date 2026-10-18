using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Onboarding;

[TestFixture]
public sealed class PremiumUpgradeTests : OnboardingTests
{
    protected override async Task PrepareScenarioAsync()
    {
        // The upsell requires at least five real ciphers, in addition to account-age eligibility.
        for (var index = 2; index <= 5; index++)
            await Seeds.CreateLoginAsync($"Premium scenario login {index}");
        await Scenario.PrepareAsync(OnboardingPrompt.Premium);
    }

    [TestCase("Continue without upgrading")]
    [TestCase("Close")]
    public async Task PremiumUpgrade_Dismiss_StaysDismissed(string dismissButton)
    {
        //Arrange
        var upgrade = Page.GetByRole("dialog").Filter(has: Page.GetByRole("heading", "Welcome to Bitwarden"));

        //Act
        await upgrade.GetByRole("button", dismissButton).ClickAsync();
        await Vault.ReloadAsync();

        //Assert
        await Expect(Vault.ItemsTable).ToBeVisibleAsync();
        await Expect(upgrade).Not.ToBeVisibleAsync();
    }
}
