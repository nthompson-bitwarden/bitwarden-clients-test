using e2e.Web.Pages;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Vault;

public class VaultBulkActionsTests : BaseTest
{
    private const string Original = "E2E login";
    private const string Control = "Vault control";
    private const string Other = "Vault second login";
    private VaultPage VaultPage { get; set; } = null!;

    public VaultBulkActionsTests()
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
        var loginPage = new LoginPage(Page);
        await Seeds.CreateLoginAsync(Control);
        await Seeds.CreateLoginAsync(Other);
        await loginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);
        await VaultPage.ItemsTable.WaitForAsync();
        await VaultPage.Item(Control).WaitForAsync();
    }

    [Test]
    public async Task BulkDelete_AfterReload_MovesOnlySelectedItemsToTrash()
    {
        //Arrange
        var first = VaultPage.Item(Original);
        var second = VaultPage.Item(Other);
        var control = VaultPage.Item(Control);

        //Act
        await VaultPage.SelectAsync(Original);
        await VaultPage.SelectAsync(Other);
        await VaultPage.BulkDeleteAsync();
        await VaultPage.ConfirmAsync("Delete");
        await first.WaitForAsync(Dramaturge.ElementState.Hidden);
        await second.WaitForAsync(Dramaturge.ElementState.Hidden);
        await VaultPage.ReloadReadyAsync();

        //Assert
        await Expect(first).ToHaveCountAsync(0);
        await Expect(second).ToHaveCountAsync(0);
        await Expect(control).ToHaveCountAsync(1);

        // Act - Open Trash
        await VaultPage.FilterAsync("Trash");

        // Assert - Verify Trash membership
        await Expect(first).ToHaveCountAsync(1);
        await Expect(second).ToHaveCountAsync(1);
        await Expect(control).ToHaveCountAsync(0);
    }
}
