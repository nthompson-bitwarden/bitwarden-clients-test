using System.Text.RegularExpressions;
using NUnit.Framework;
using e2e.Authentication;
using e2e.Web.Pages;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Authentication;

public sealed class EmailTwoFactorTests : BaseTest
{
    private TwoFactorEnrollment Enrollment = null!;
    private LoginPage LoginPage = null!;
    private TwoFactorPage TwoFactorPage = null!;
    private VaultPage VaultPage = null!;

    [SetUp]
    public async Task EnrollEmailAsync()
    {
        LoginPage = new LoginPage(Page);
        TwoFactorPage = new TwoFactorPage(Page);
        VaultPage = new VaultPage(Page);
        Enrollment = new TwoFactorEnrollment(ServerUrl, Seeds);
        await Enrollment.AuthenticateAsync();
        await EnrollEmailWithCleanupAsync();
    }

    private async Task EnrollEmailWithCleanupAsync()
    {
        try
        {
            await Enrollment.EnrollEmailAsync();
        }
        catch
        {
            // NUnit skips this fixture's teardown when its setup fails.
            Enrollment.Dispose();
            throw;
        }
    }

    [TearDown]
    public void DisposeEnrollment() => Enrollment?.Dispose();

    [Test]
    public async Task LogIn_WithEmailCode_IsSuccessful()
    {
        //Arrange
        var issuedCode = new Regex(@"/api/two-factor/send-email-login(?:[?]|$)");

        //Act
        await Page.RunAndWaitForResponseAsync(() => LoginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password), issuedCode);
        await TwoFactorPage.VerificationCode.WaitForAsync();
        var code = await Seeds.GetEmailCodeAsync();
        await TwoFactorPage.SubmitCodeAsync(code);

        //Assert
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/vault(?:[/?]|$)"));
        await Expect(VaultPage.ItemsTable).ToBeVisibleAsync();
        await Expect(Page.GetByText("E2E login", exact: true)).ToBeVisibleAsync();
        await Expect(VaultPage.MasterPassword).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task LogIn_WithInvalidEmailCode_Fails()
    {
        //Arrange
        var issuedCode = new Regex(@"/api/two-factor/send-email-login(?:[?]|$)");

        //Act
        await Page.RunAndWaitForResponseAsync(() => LoginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password), issuedCode);
        await TwoFactorPage.VerificationCode.WaitForAsync();
        var code = await Seeds.GetEmailCodeAsync();
        var invalidCode = code == "000000" ? "000001" : "000000";
        await TwoFactorPage.SubmitCodeAsync(invalidCode);

        //Assert
        await Expect(TwoFactorPage.InvalidCode).ToBeVisibleAsync();
        await Expect(TwoFactorPage.VerificationCode).ToBeVisibleAsync();
        await Expect(TwoFactorPage.ContinueButton).ToBeEnabledAsync();
        await Expect(VaultPage.ItemsTable).Not.ToBeVisibleAsync();
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/2fa(?:[/?]|$)"));
    }
}
