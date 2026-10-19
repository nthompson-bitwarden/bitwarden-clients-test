using e2e.Web.Components;
using e2e.Web.Pages;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Vault;

public class VaultFolderTests : BaseTest
{
    private const string Original = "E2E login";
    private const string Control = "Vault control";
    private VaultPage VaultPage { get; set; } = null!;
    private VaultItemDialog VaultItemDialogComponent { get; set; } = null!;
    private VaultFolderDialog VaultFolderDialogComponent { get; set; } = null!;

    public VaultFolderTests()
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
        VaultFolderDialogComponent = new VaultFolderDialog(Page);
        var loginPage = new LoginPage(Page);
        await Seeds.CreateLoginAsync(Control);
        await Seeds.CreateLoginAsync("Vault second login");
        await loginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);
        await VaultPage.ItemsTable.WaitForAsync();
        await VaultPage.Item(Control).WaitForAsync();
    }

    [Test]
    public async Task CreateFolderAndMoveLogin_AfterReload_PreservesMembership()
    {
        //Arrange
        var folderName = "Folder " + Guid.NewGuid().ToString("N");

        //Act
        // Act - Create a folder and assign the existing item
        await VaultPage.NewItemAsync("Folder");
        await VaultFolderDialogComponent.CreateAsync(folderName);
        await VaultPage.EditItemAsync(Original);
        await VaultItemDialogComponent.SelectAsync("Folder", folderName);
        await VaultItemDialogComponent.SaveAsync();

        // Act - Read persisted membership through the folder filter and item form
        await VaultPage.ReloadReadyAsync();
        await VaultPage.FilterAsync(folderName);
        await VaultPage.EditItemAsync(Original);

        var folderPreserved = await VaultItemDialogComponent.SelectedFolderMatchesAsync(folderName);
        await VaultItemDialogComponent.CancelAsync();

        //Assert
        await Expect(VaultPage.Item(Original)).ToHaveCountAsync(1);
        await Expect(VaultPage.Item(Control)).ToHaveCountAsync(0);
        Assert.That(folderPreserved, Is.True, "Assigned folder must persist after reload.");
    }
}
