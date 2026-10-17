using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Onboarding;

[TestFixture]
public sealed class NewExperienceTests : OnboardingTests
{
    protected override Task PrepareScenarioAsync() =>
        Scenario.PrepareAsync(OnboardingPrompt.NewExperience, redesignedVault: true);

    [Test]
    public async Task NewExperience_Close_OffersWalkthroughInsteadOfRepeatingAnnouncement()
    {
        //Arrange
        var announcement = Page.GetByRole("dialog", "Bitwarden has a new look");
        var explore = Page.GetByRole("button").Filter(hasText: "Explore the new vault");

        //Act
        await announcement.GetByRole("button", "Close").ClickAsync();
        await Vault.ReloadAsync();

        //Assert
        await Expect(explore).ToBeVisibleAsync();
        await Expect(announcement).Not.ToBeVisibleAsync();
    }
}
