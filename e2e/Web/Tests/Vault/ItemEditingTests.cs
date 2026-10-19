using e2e.Web.Components;
using e2e.Web.Pages;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Vault;

public class ItemEditingTests : BaseTest
{
    private const string Original = "E2E login";
    private const string Control = "Vault control";
    private VaultPage VaultPage { get; set; } = null!;
    private VaultItemDialog VaultItemDialogComponent { get; set; } = null!;

    public ItemEditingTests()
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
    public async Task EditLogin_AfterReload_PreservesRenamedItemAndFields()
    {
        //Arrange
        var name = "Renamed " + Guid.NewGuid().ToString("N");
        var fields = new Dictionary<string, string>
        {
            ["Username"] = "updated-user",
            ["Website (URI)"] = "https://updated.example.com"
        };

        //Act
        await VaultPage.EditItemAsync(Original);
        await VaultItemDialogComponent.Name.FillAsync(name);
        await VaultItemDialogComponent.FillFieldsAsync(fields);
        await VaultItemDialogComponent.SaveAsync();
        await VaultPage.ReloadReadyAsync();
        await VaultPage.EditItemAsync(name);
        var fieldsPreserved = await VaultItemDialogComponent.FieldsMatchAsync(fields);

        await VaultItemDialogComponent.CancelAsync();

        //Assert
        Assert.That(fieldsPreserved, Is.True, "Edited login fields must persist.");
        await Expect(VaultPage.Item(name)).ToHaveCountAsync(1);
        await Expect(VaultPage.Item(Original)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task CloneLogin_EditingClone_PreservesIndependentOriginal()
    {
        //Arrange
        var name = "Clone " + Guid.NewGuid().ToString("N");
        var originalFields = new Dictionary<string, string>
        {
            ["Username"] = "e2e",
            ["Website (URI)"] = "https://example.com"
        };
        var cloneFields = new Dictionary<string, string> { ["Username"] = "clone-only-user" };

        //Act
        // Act - Clone and read copied fields after reload
        await VaultPage.ItemActionAsync(Original, "Clone");
        await VaultItemDialogComponent.Name.FillAsync(name);
        await VaultItemDialogComponent.SaveAsync();
        await VaultPage.ReloadReadyAsync();
        await VaultPage.EditItemAsync(name);
        var copiedFields = await VaultItemDialogComponent.FieldsMatchAsync(originalFields);

        // Act - Change only the clone and read both independent records
        await VaultItemDialogComponent.FillFieldsAsync(cloneFields);
        await VaultItemDialogComponent.SaveAsync();
        await VaultPage.ReloadReadyAsync();
        await VaultPage.EditItemAsync(name);
        var cloneUpdated = await VaultItemDialogComponent.FieldsMatchAsync(cloneFields);
        await VaultItemDialogComponent.CancelAsync();
        await VaultPage.EditItemAsync(Original);
        var originalUnchanged = await VaultItemDialogComponent.FieldsMatchAsync(originalFields);

        await VaultItemDialogComponent.CancelAsync();

        //Assert
        Assert.That(copiedFields, Is.True, "Clone must copy the original login fields.");
        Assert.That(cloneUpdated, Is.True, "Clone edit must persist.");
        Assert.That(originalUnchanged, Is.True, "Editing the clone must preserve the original fields.");
        await Expect(VaultPage.Item(Original)).ToHaveCountAsync(1);
        await Expect(VaultPage.Item(name)).ToHaveCountAsync(1);
    }
}
