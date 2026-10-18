using System.Text.RegularExpressions;
using NUnit.Framework;

namespace e2e.Web.Tests.Authentication;

public sealed class EmailTwoFactorTests : TwoFactorAuthenticationTests
{
    [SetUp]
    public async Task EnrollEmailAsync()
    {
        await Enrollment.EnrollEmailAsync();
    }

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
        await AssertVaultIsUnlockedAsync();
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
        await AssertCodeWasRejectedAsync();
    }
}
