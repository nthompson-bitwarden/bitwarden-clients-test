using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Onboarding;

[TestFixture]
public sealed class ChecklistTests : OnboardingTests
{
    protected override Task PrepareScenarioAsync() =>
        Scenario.PrepareAsync(OnboardingPrompt.Checklist);

    [Test]
    public async Task Checklist_Dismiss_StaysDismissed()
    {
        //Arrange
        var itemsTable = Vault.ItemsTable;
        var checklist = Page.GetByRole("button").Filter(hasText: "Get started with your vault");

        //Act
        if (await checklist.GetAttributeAsync("aria-expanded") == "false")
            await checklist.ClickAsync();
        await Page.GetByRole("button", "Dismiss this checklist").ClickAsync();
        await Vault.ReloadAsync();

        //Assert
        await Expect(itemsTable).ToBeVisibleAsync();
        await Expect(Page.GetByText("Get started with your vault", exact: true)).Not.ToBeVisibleAsync();
    }
}
