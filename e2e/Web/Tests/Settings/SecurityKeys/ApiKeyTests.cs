using e2e.Web.Components;
using e2e.Web.Pages;
using System.Text.RegularExpressions;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Settings.SecurityKeys;

public class ApiKeyTests : BaseTest
{
    private SecurityKeysPage SecurityKeysPage { get; set; } = null!;
    private ApiKeyDialog ApiKeyDialogComponent { get; set; } = null!;

    public ApiKeyTests()
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
        ApiKeyDialogComponent = new ApiKeyDialog(Page);
        var loginPage = new LoginPage(Page);
        var vaultPage = new VaultPage(Page);
        await loginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);
        await vaultPage.ItemsTable.WaitForAsync();
        await SecurityKeysPage.OpenAsync();
    }

    [Test]
    public async Task ViewApiKey_AfterVerification_ShowsStableAccountCredentials()
    {
        //Arrange
        var expectedClientId = "user." + Account.UserId;

        //Act
        // Act - Observe the verification gate
        await SecurityKeysPage.ViewApiKeyAsync();
        await ApiKeyDialogComponent.MasterPassword.WaitForAsync();
        var credentialsHidden = await ApiKeyDialogComponent.ClientId.CountAsync() == 0 &&
            await ApiKeyDialogComponent.ClientSecret.CountAsync() == 0;

        // Act - Verify and read the account credentials
        await ApiKeyDialogComponent.VerifyAsync(Account.Password);
        var clientId = await ApiKeyDialogComponent.ClientId.TextContentAsync();
        var secret = await ApiKeyDialogComponent.ClientSecret.TextContentAsync();
        var scope = await ApiKeyDialogComponent.Scope.TextContentAsync();
        var grantType = await ApiKeyDialogComponent.GrantType.TextContentAsync();
        await ApiKeyDialogComponent.CloseAsync();

        // Act - Reopen and verify the key again
        await SecurityKeysPage.ViewApiKeyAsync();
        await ApiKeyDialogComponent.MasterPassword.WaitForAsync();
        var requiresFreshVerification = await ApiKeyDialogComponent.ClientSecret.CountAsync() == 0;
        await ApiKeyDialogComponent.VerifyAsync(Account.Password);
        var reopenedSecret = await ApiKeyDialogComponent.ClientSecret.TextContentAsync();
        await ApiKeyDialogComponent.CloseAsync();

        //Assert
        // Assert - Confirm navigation and section rendering
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/settings/security/security-keys$"));
        await Expect(SecurityKeysPage.EncryptionSettings).ToBeVisibleAsync();
        await Expect(SecurityKeysPage.ApiKeyHeading).ToBeVisibleAsync();

        // Assert - Confirm verification and stable account credentials
        Assert.That(credentialsHidden, Is.True, "Credentials must be hidden before verification.");
        Assert.That(requiresFreshVerification, Is.True, "Reopening must require fresh verification.");
        Assert.That(clientId == expectedClientId, Is.True, "API client ID must match this account.");
        Assert.That(!string.IsNullOrWhiteSpace(secret), Is.True, "API secret must be present.");
        Assert.That(scope == "api", Is.True, "API scope must match.");
        Assert.That(grantType == "client_credentials", Is.True, "API grant type must match.");
        Assert.That(secret == reopenedSecret, Is.True, "Viewing must preserve the API secret.");
    }

    [Test]
    public async Task RotateApiKey_AfterVerification_PersistsReplacement()
    {
        //Arrange
        // Arrange - Read the original account credentials
        await SecurityKeysPage.ViewApiKeyAsync();
        await ApiKeyDialogComponent.VerifyAsync(Account.Password);
        var originalSecret = await ApiKeyDialogComponent.ClientSecret.TextContentAsync();
        var originalClientId = await ApiKeyDialogComponent.ClientId.TextContentAsync();
        await ApiKeyDialogComponent.CloseAsync();

        //Act
        // Act - Rotate the key after verification
        await SecurityKeysPage.RotateApiKeyAsync();
        await ApiKeyDialogComponent.VerifyAsync(Account.Password, rotate: true);
        var replacement = await ApiKeyDialogComponent.ClientSecret.TextContentAsync();
        var rotatedClientId = await ApiKeyDialogComponent.ClientId.TextContentAsync();
        await ApiKeyDialogComponent.CloseAsync();

        // Act - Retrieve the key independently after reloading
        await Page.ReloadAsync();
        await SecurityKeysPage.EncryptionSettings.WaitForAsync();
        await SecurityKeysPage.ViewApiKeyAsync();
        await ApiKeyDialogComponent.VerifyAsync(Account.Password);
        var persistedSecret = await ApiKeyDialogComponent.ClientSecret.TextContentAsync();
        await ApiKeyDialogComponent.CloseAsync();

        //Assert
        // Assert - Confirm replacement and account identity
        Assert.That(!string.IsNullOrWhiteSpace(originalSecret), Is.True, "Original API secret must be present.");
        Assert.That(!string.IsNullOrWhiteSpace(replacement), Is.True, "Replacement API secret must be present.");
        Assert.That(replacement != originalSecret, Is.True, "Rotation must replace the API secret.");
        Assert.That(rotatedClientId == originalClientId, Is.True, "Rotation must preserve the client ID.");

        // Assert - Confirm the replacement persisted
        Assert.That(persistedSecret == replacement, Is.True, "Fresh retrieval must return the replacement.");
    }
}
