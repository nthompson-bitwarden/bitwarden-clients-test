using System.Text.RegularExpressions;
using NUnit.Framework;
using e2e.Authentication;
using e2e.Web.Pages;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Authentication;

public sealed class TotpTwoFactorTests : BaseTest
{
    private TwoFactorEnrollment Enrollment = null!;
    private LoginPage LoginPage = null!;
    private TwoFactorPage TwoFactorPage = null!;
    private VaultPage VaultPage = null!;

    [SetUp]
    public async Task EnrollTotpAsync()
    {
        LoginPage = new LoginPage(Page);
        TwoFactorPage = new TwoFactorPage(Page);
        VaultPage = new VaultPage(Page);
        Enrollment = new TwoFactorEnrollment(ServerUrl, Seeds);
        await Enrollment.AuthenticateAsync();
        await EnrollTotpWithCleanupAsync();
    }

    private async Task EnrollTotpWithCleanupAsync()
    {
        try
        {
            await Enrollment.EnrollTotpAsync();
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
    public async Task LogIn_WithAuthenticatorCode_IsSuccessful()
    {
        //Arrange
        var twoFactorPage = TwoFactorPage;

        //Act
        await LoginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);
        await twoFactorPage.SubmitCodeAsync(Enrollment.LoginTotpCode());

        //Assert
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/vault(?:[/?]|$)"));
        await Expect(VaultPage.ItemsTable).ToBeVisibleAsync();
        await Expect(Page.GetByText("E2E login", exact: true)).ToBeVisibleAsync();
        await Expect(VaultPage.MasterPassword).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task LogIn_WithInvalidAuthenticatorCode_Fails()
    {
        //Arrange
        var invalidCode = Enrollment.InvalidTotpCode();

        //Act
        await LoginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);
        await TwoFactorPage.SubmitCodeAsync(invalidCode);

        //Assert
        await Expect(TwoFactorPage.InvalidCode).ToBeVisibleAsync();
        await Expect(TwoFactorPage.VerificationCode).ToBeVisibleAsync();
        await Expect(TwoFactorPage.ContinueButton).ToBeEnabledAsync();
        await Expect(VaultPage.ItemsTable).Not.ToBeVisibleAsync();
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/2fa(?:[/?]|$)"));
    }
}
