using e2e.Web.Components;
using e2e.Web.Pages;
using NUnit.Framework;

namespace e2e.Web.Tests.Settings.SecurityKeys;

public abstract class SecurityKeysTestsBase : BaseTest
{
    protected SecurityKeysPage SecurityKeysPage { get; private set; } = null!;
    protected VaultPage VaultPage { get; private set; } = null!;
    protected LoginPage LoginPage { get; private set; } = null!;
    protected ApiKeyDialog ApiKeyDialogComponent { get; private set; } = null!;
    protected KdfConfirmationDialog KdfConfirmationDialogComponent { get; private set; } = null!;

    protected SecurityKeysTestsBase()
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
        ApiKeyDialogComponent = new ApiKeyDialog(Page);
        KdfConfirmationDialogComponent = new KdfConfirmationDialog(Page);
        await LoginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);
        await VaultPage.ItemsTable.WaitForAsync();
        await SecurityKeysPage.OpenAsync();
    }
}
