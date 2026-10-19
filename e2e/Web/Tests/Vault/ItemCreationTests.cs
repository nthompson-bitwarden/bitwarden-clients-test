using e2e.Web.Components;
using e2e.Web.Pages;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Vault;

public class ItemCreationTests : BaseTest
{
    private const string Original = "E2E login";
    private const string Control = "Vault control";
    private VaultPage VaultPage { get; set; } = null!;
    private VaultItemDialog VaultItemDialogComponent { get; set; } = null!;

    public ItemCreationTests()
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

    [TestCase("Login")]
    [TestCase("Secure note")]
    [TestCase("Card")]
    [TestCase("Identity")]
    public async Task CreateItem_AfterReload_PreservesTypeAndFields(string type)
    {
        //Arrange
        var name = "Created " + Guid.NewGuid().ToString("N");
        var fields = type switch
        {
            "Login" => new Dictionary<string, string> { ["Username"] = "created-user", ["Password"] = "Synthetic-only-password!", ["Website (URI)"] = "https://created.example.com" },
            "Secure note" => new Dictionary<string, string> { ["Notes"] = "Synthetic vault note" },
            "Card" => new Dictionary<string, string> { ["Cardholder name"] = "Test Holder", ["Number"] = "4111111111111111", ["Expiration year"] = "2030" },
            "Identity" => new Dictionary<string, string> { ["First name"] = "Test", ["Last name"] = "Identity", ["Email"] = "identity@example.com" },
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        var expirationMonth = type == "Card" ? "01 - January" : null;

        //Act
        // Act - Create a representative item through its type-specific form
        await VaultPage.NewItemAsync(type);
        await VaultItemDialogComponent.Name.FillAsync(name);
        await VaultItemDialogComponent.FillFieldsAsync(fields, expirationMonth);
        await VaultItemDialogComponent.SaveAsync();

        // Act - Read back the persisted type-specific form after a reload
        await VaultPage.ReloadReadyAsync();
        await VaultPage.EditItemAsync(name);
        var valuesPreserved = await VaultItemDialogComponent.FieldsMatchAsync(fields, expirationMonth);

        //Assert
        // Keep decrypted field values out of assertion diagnostics.
        Assert.That(valuesPreserved, Is.True, "Created item fields and selected options must persist.");
        await Expect(VaultItemDialogComponent.EditTitle(type)).ToBeVisibleAsync();
        await Expect(VaultItemDialogComponent.Name).ToHaveValueAsync(name);
    }
}
