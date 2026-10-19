using e2e.Web.Components;
using e2e.Web.Pages;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Vault;

public class VaultTrashTests : BaseTest
{
    private const string Original = "E2E login";
    private const string Control = "Vault control";
    private VaultPage VaultPage { get; set; } = null!;
    private VaultItemDialog VaultItemDialogComponent { get; set; } = null!;

    public VaultTrashTests()
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
    public async Task DeleteLogin_AfterReload_MovesOnlyDeletedItemToTrash()
    {
        //Arrange
        var deletedItem = VaultPage.Item(Original);
        var control = VaultPage.Item(Control);

        //Act
        await VaultPage.DeleteItemAsync(Original);
        await VaultPage.ReloadReadyAsync();

        //Assert
        await Expect(deletedItem).ToHaveCountAsync(0);
        await Expect(control).ToHaveCountAsync(1);

        // Act - Open Trash
        await VaultPage.FilterAsync("Trash");

        // Assert - Verify Trash membership
        await Expect(deletedItem).ToHaveCountAsync(1);
        await Expect(control).ToHaveCountAsync(0);
    }

    [Test]
    public async Task RestoreLogin_AfterReload_ReturnsReadableItemToVault()
    {
        //Arrange
        await VaultPage.DeleteItemAsync(Original);
        await VaultPage.FilterAsync("Trash");

        //Act
        await VaultPage.ItemActionAsync(Original, "Restore");
        await VaultPage.Item(Original).WaitForAsync(Dramaturge.ElementState.Hidden);
        await VaultPage.ReloadReadyAsync();

        await VaultPage.EmptyTrash.WaitForAsync();

        //Assert
        await Expect(VaultPage.Item(Original)).ToHaveCountAsync(0);

        // Act - Return to all items
        await VaultPage.FilterAsync("All items");

        // Assert - Verify active vault membership
        await Expect(VaultPage.Item(Original)).ToHaveCountAsync(1);
        await Expect(VaultPage.Item(Control)).ToHaveCountAsync(1);

        // Act - Read the restored item
        await VaultPage.OpenSeededLoginAsync();
        var readable = await VaultItemDialogComponent.ReadOnlyLoginMatchesAsync("e2e", "example.com");

        // Assert - Restored fields remain readable
        Assert.That(readable, Is.True, "Restored login must remain readable.");
    }

    [Test]
    public async Task PermanentlyDeleteLogin_AfterReload_RemovesItemFromTrashAndVault()
    {
        //Arrange
        await VaultPage.DeleteItemAsync(Original);
        await VaultPage.FilterAsync("Trash");

        //Act
        await VaultPage.ItemActionAsync(Original, "Permanently delete");
        await VaultPage.ConfirmAsync("Yes");
        await VaultPage.Item(Original).WaitForAsync(Dramaturge.ElementState.Hidden);
        await VaultPage.ReloadReadyAsync();

        await VaultPage.EmptyTrash.WaitForAsync();

        //Assert
        await Expect(VaultPage.Item(Original)).ToHaveCountAsync(0);

        // Act - Return to all items
        await VaultPage.FilterAsync("All items");

        // Assert - Verify active vault membership
        await Expect(VaultPage.Item(Original)).ToHaveCountAsync(0);
        await Expect(VaultPage.Item(Control)).ToHaveCountAsync(1);
    }
}
