using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Onboarding;

[TestFixture(true)]
public sealed class WalkthroughTests(bool fromAnnouncement) : OnboardingTests
{
    protected override Task PrepareScenarioAsync() =>
        Scenario.PrepareAsync(fromAnnouncement ? OnboardingPrompt.NewExperience : OnboardingPrompt.Walkthrough, redesignedVault: true);

    [Test]
    public async Task Walkthrough_Complete_DismissesAnnouncementAndCallout()
    {
        //Arrange
        var itemsTable = Vault.ItemsTable;
        var explore = Page.GetByRole("button").Filter(hasText: "Explore the new vault");

        //Act
        await explore.ClickAsync();
        await Page.GetByText("Switch between products", exact: true).WaitForAsync();
        await Page.GetByRole("button", "Next").ClickAsync();
        await Page.GetByRole("dialog", "Mix and match filters").WaitForAsync();
        await Page.GetByRole("button", "Done").ClickAsync();
        await Vault.ReloadAsync();

        //Assert
        await Expect(itemsTable).ToBeVisibleAsync();
        await Expect(Page.GetByRole("dialog", "Bitwarden has a new look")).Not.ToBeVisibleAsync();
        await Expect(explore).Not.ToBeVisibleAsync();
    }
}
