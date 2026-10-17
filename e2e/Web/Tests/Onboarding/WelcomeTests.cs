using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Onboarding;

[TestFixture(true)]
public sealed class WelcomeTests(bool redesignedVault) : OnboardingTests
{
    protected override Task PrepareScenarioAsync() =>
        Scenario.PrepareAsync(OnboardingPrompt.Welcome, redesignedVault);

    [Test]
    public async Task Welcome_Skip_StaysDismissed()
    {
        //Arrange
        var welcome = Page.GetByRole("dialog").Filter(has: Page.GetByRole("heading", "You're in! Welcome to Bitwarden"));

        //Act
        await welcome.GetByRole("button", "Skip").ClickAsync();
        await Vault.ReloadAsync();

        //Assert
        await Expect(Vault.ItemsTable).ToBeVisibleAsync();
        await Expect(welcome).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task Welcome_StartTour_WalksThroughPersonalVault()
    {
        //Arrange
        var itemsTable = Vault.ItemsTable;

        //Act
        await Page.GetByRole("button", "Start tour").ClickAsync();
        await Page.GetByText("Quickly import your passwords", exact: true).WaitForAsync();
        await Page.GetByRole("button", "Next").ClickAsync();
        await Page.GetByRole("dialog", "Add an item").WaitForAsync();
        await Page.GetByRole("button", "Next").ClickAsync();
        await Page.GetByText("Monitor your password security", exact: true).WaitForAsync();
        await Page.GetByRole("button", "Done").ClickAsync();
        await Vault.ReloadAsync();

        //Assert
        await Expect(itemsTable).ToBeVisibleAsync();
        await Expect(Page.GetByRole("button", "Start tour")).Not.ToBeVisibleAsync();
        await Expect(Page.GetByText("Monitor your password security", exact: true)).Not.ToBeVisibleAsync();
    }
}
