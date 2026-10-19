using e2e.Web.Components;
using e2e.Web.Pages;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Vault;

public class VaultFilteringTests : BaseTest
{
    private const string Original = "E2E login";
    private const string Control = "Vault control";
    private const string Other = "Vault second login";
    private VaultPage VaultPage { get; set; } = null!;
    private VaultItemDialog VaultItemDialogComponent { get; set; } = null!;

    public VaultFilteringTests()
    {
        // Decrypted fields must never be captured, including on failed assertions.
        ScreenshotOnFailure = false;
        VideoOnFailure = false;
        TraceOnFailure = false;
    }

    [SetUp]
    public async Task OpenVaultAsync()
    {
        VaultPage = new VaultPage(Page);
        VaultItemDialogComponent = new VaultItemDialog(Page);
        var loginPage = new LoginPage(Page);
        await Seeds.CreateLoginAsync(Control);
        await Seeds.CreateLoginAsync("Vault second login");
        await loginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);
        await VaultPage.ItemsTable.WaitForAsync();
        await VaultPage.Item(Control).WaitForAsync();
    }

    [Test]
    public async Task SearchAndClear_ReturnsMatchingItemsThenAllItems()
    {
        //Arrange
        var matchingItem = VaultPage.Item(Control);
        var distractor = VaultPage.Item(Original);

        //Act
        await VaultPage.Search.FillAsync("control");

        //Assert
        await Expect(matchingItem).ToHaveCountAsync(1);
        await Expect(distractor).ToHaveCountAsync(0);

        // Act - Clear the query
        await VaultPage.Search.FillAsync("");

        // Assert - Both saved items return
        await Expect(matchingItem).ToHaveCountAsync(1);
        await Expect(distractor).ToHaveCountAsync(1);
    }

    [Test]
    public async Task ItemTypeFilters_ExcludeOtherTypesAndReturnAllItems()
    {
        //Arrange
        var noteName = "Note " + Guid.NewGuid().ToString("N");
        await VaultPage.NewItemAsync("Secure note");
        await VaultItemDialogComponent.Name.FillAsync(noteName);
        await VaultItemDialogComponent.Field("Notes").FillAsync("Type filter note");
        await VaultItemDialogComponent.SaveAsync();
        await VaultPage.ReloadReadyAsync();

        //Act
        await VaultPage.FilterAsync("Secure note");

        //Assert
        await Expect(VaultPage.Item(noteName)).ToHaveCountAsync(1);
        await Expect(VaultPage.Item(Original)).ToHaveCountAsync(0);

        // Act - Select logins
        await VaultPage.FilterAsync("Login");

        // Assert - The login filter excludes the note
        await Expect(VaultPage.Item(Original)).ToHaveCountAsync(1);
        await Expect(VaultPage.Item(noteName)).ToHaveCountAsync(0);

        // Act - Return to all items
        await VaultPage.FilterAsync("All items");

        // Assert - All saved types return
        await Expect(VaultPage.Item(Original)).ToHaveCountAsync(1);
        await Expect(VaultPage.Item(noteName)).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Favorite_AfterReload_PersistsAndCanBeRemoved()
    {
        //Arrange
        var favorite = VaultPage.Item(Original);
        var control = VaultPage.Item(Control);

        //Act
        await VaultPage.ItemActionAsync(Original, "Favorite");
        await VaultPage.ReloadReadyAsync();
        await VaultPage.FilterAsync("Favorites");

        //Assert
        await Expect(favorite).ToHaveCountAsync(1);
        await Expect(control).ToHaveCountAsync(0);

        // Act - Remove the persisted favorite without deleting the item
        await VaultPage.ItemActionAsync(Original, "Unfavorite");
        await VaultPage.ReloadReadyAsync();
        await VaultPage.EmptyFavorites.WaitForAsync();

        // Assert - Favorites excludes the unfavorited item
        await Expect(favorite).ToHaveCountAsync(0);

        // Act - Return to all items
        await VaultPage.FilterAsync("All items");

        // Assert - Removing a favorite preserves the item
        await Expect(favorite).ToHaveCountAsync(1);
        await Expect(control).ToHaveCountAsync(1);
    }
}
