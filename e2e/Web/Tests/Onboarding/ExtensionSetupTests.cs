using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Onboarding;

[TestFixture]
public sealed class ExtensionSetupTests : OnboardingTests
{
    protected override Task PrepareScenarioAsync() =>
        Scenario.PrepareAsync(OnboardingPrompt.ExtensionSetup);

    [Test]
    public async Task ExtensionSetup_AddItLater_ContinuesToVaultAndStaysDismissed()
    {
        //Arrange
        var itemsTable = Vault.ItemsTable;

        //Act
        await Page.GetByRole("button", "Add it later").ClickAsync();
        await Page.GetByRole("dialog").GetByRole("link", "Skip to web app").ClickAsync();
        await itemsTable.WaitForAsync();
        await Vault.ReloadAsync();

        //Assert
        await Expect(itemsTable).ToBeVisibleAsync();
        await Expect(Page.GetByRole("heading", "Autofill your passwords securely with one click")).Not.ToBeVisibleAsync();
    }
}
