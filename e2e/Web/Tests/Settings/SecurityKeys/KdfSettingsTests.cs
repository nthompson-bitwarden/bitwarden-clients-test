using e2e.Web.Components;
using e2e.Web.Pages;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Settings.SecurityKeys;

public class KdfSettingsTests : BaseTest
{
    private SecurityKeysPage SecurityKeysPage { get; set; } = null!;
    private VaultPage VaultPage { get; set; } = null!;
    private LoginPage LoginPage { get; set; } = null!;
    private KdfConfirmationDialog KdfConfirmationDialogComponent { get; set; } = null!;

    public KdfSettingsTests()
    {
        // These journeys reveal credentials and decrypted vault data, including on failure paths.
        ScreenshotOnFailure = false;
        VideoOnFailure = false;
        TraceOnFailure = false;
    }

    [SetUp]
    public async Task OpenSecurityKeysAsync()
    {
        SecurityKeysPage = new SecurityKeysPage(Page);
        VaultPage = new VaultPage(Page);
        LoginPage = new LoginPage(Page);
        KdfConfirmationDialogComponent = new KdfConfirmationDialog(Page);
        await LoginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);
        await VaultPage.ItemsTable.WaitForAsync();
        await SecurityKeysPage.OpenAsync();
    }

    [Test]
    public async Task UpdatePbkdf2Settings_AfterFreshLogin_PreservesVaultAndParameters()
    {
        //Arrange
        var currentIterations = await SecurityKeysPage.Iterations.InputValueAsync();
        var iterations = currentIterations == "600000" ? "600001" : "600000";

        //Act
        // Act - Set the encryption parameters
        await SecurityKeysPage.SelectAlgorithmAsync("PBKDF2 SHA-256");
        await SecurityKeysPage.Iterations.FillAsync(iterations);

        // Act - Save with master password confirmation
        await SecurityKeysPage.UpdateAsync();
        await KdfConfirmationDialogComponent.ConfirmAsync(Account.Password);
        await SecurityKeysPage.Saved.WaitForAsync();
        var confirmationClosed = await KdfConfirmationDialogComponent.Root.CountAsync() == 0;

        // Act - Authenticate again using the persisted KDF
        await VaultPage.LogOutAsync();
        await LoginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);
        await VaultPage.ItemsTable.WaitForAsync();

        // Act - Read the decrypted seeded item and folder
        await VaultPage.OpenSeededLoginAsync();
        var username = await VaultPage.LoginUsername.InputValueAsync();
        var website = await VaultPage.LoginWebsite.InputValueAsync();
        await VaultPage.LoginFolder.WaitForAsync();
        var folderReadable = await VaultPage.LoginFolder.CountAsync() == 1;
        await VaultPage.CloseItemAsync();

        // Act - Return to the saved encryption settings
        await SecurityKeysPage.OpenAsync();

        //Assert
        // Assert - Confirm successful save and readable vault data
        Assert.That(confirmationClosed, Is.True, "KDF confirmation must close after saving.");
        Assert.That(username == "e2e", Is.True, "Seeded login username must remain readable.");
        // The read-only view displays the host derived from the seeded https://example.com URI.
        Assert.That(website == "example.com", Is.True, "Seeded login website must remain readable.");
        Assert.That(folderReadable, Is.True, "Seeded login folder must remain readable.");

        // Assert - Confirm all encryption parameters persisted
        await Expect(SecurityKeysPage.SelectedAlgorithm).ToHaveTextAsync("PBKDF2 SHA-256");
        await Expect(SecurityKeysPage.Iterations).ToHaveValueAsync(iterations);
    }

    [Test]
    public async Task SwitchToArgon2id_AfterFreshLogin_PreservesVaultAndParameters()
    {
        //Arrange
        const string iterations = "3";
        const string memory = "64";
        const string parallelism = "4";

        //Act
        // Act - Set the encryption parameters
        await SecurityKeysPage.SelectAlgorithmAsync("Argon2id");
        await SecurityKeysPage.Iterations.FillAsync(iterations);
        await SecurityKeysPage.Memory.FillAsync(memory);
        await SecurityKeysPage.Parallelism.FillAsync(parallelism);

        // Act - Save with master password confirmation
        await SecurityKeysPage.UpdateAsync();
        await KdfConfirmationDialogComponent.ConfirmAsync(Account.Password);
        await SecurityKeysPage.Saved.WaitForAsync();
        var confirmationClosed = await KdfConfirmationDialogComponent.Root.CountAsync() == 0;

        // Act - Authenticate again using the persisted KDF
        await VaultPage.LogOutAsync();
        await LoginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);
        await VaultPage.ItemsTable.WaitForAsync();

        // Act - Read the decrypted seeded item and folder
        await VaultPage.OpenSeededLoginAsync();
        var username = await VaultPage.LoginUsername.InputValueAsync();
        var website = await VaultPage.LoginWebsite.InputValueAsync();
        await VaultPage.LoginFolder.WaitForAsync();
        var folderReadable = await VaultPage.LoginFolder.CountAsync() == 1;
        await VaultPage.CloseItemAsync();

        // Act - Return to the saved encryption settings
        await SecurityKeysPage.OpenAsync();

        //Assert
        // Assert - Confirm successful save and readable vault data
        Assert.That(confirmationClosed, Is.True, "KDF confirmation must close after saving.");
        Assert.That(username == "e2e", Is.True, "Seeded login username must remain readable.");
        Assert.That(website == "example.com", Is.True, "Seeded login website must remain readable.");
        Assert.That(folderReadable, Is.True, "Seeded login folder must remain readable.");

        // Assert - Confirm all encryption parameters persisted
        await Expect(SecurityKeysPage.SelectedAlgorithm).ToHaveTextAsync("Argon2id");
        await Expect(SecurityKeysPage.Iterations).ToHaveValueAsync(iterations);
        await Expect(SecurityKeysPage.Memory).ToHaveValueAsync(memory);
        await Expect(SecurityKeysPage.Parallelism).ToHaveValueAsync(parallelism);
    }
}
