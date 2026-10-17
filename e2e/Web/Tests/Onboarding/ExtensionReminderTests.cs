using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Onboarding;

[TestFixture]
public sealed class ExtensionReminderTests : OnboardingTests
{
    protected override Task PrepareScenarioAsync() =>
        Scenario.PrepareAsync(OnboardingPrompt.ExtensionReminder);

    [Test]
    public async Task ExtensionReminder_Skip_StaysDismissed()
    {
        //Arrange
        var reminder = Page.GetByRole("dialog").Filter(has: Page.GetByRole("heading", "Get the extension for easy vault access"));

        //Act
        await reminder.GetByRole("button", "Skip").ClickAsync();
        await Vault.ReloadAsync();

        //Assert
        await Expect(Vault.ItemsTable).ToBeVisibleAsync();
        await Expect(reminder).Not.ToBeVisibleAsync();
    }
}
