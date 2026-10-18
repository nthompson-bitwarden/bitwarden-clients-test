using NUnit.Framework;

namespace e2e.Web.Tests.Authentication;

public sealed class TotpTwoFactorTests : TwoFactorAuthenticationTests
{
    [SetUp]
    public async Task EnrollTotpAsync()
    {
        await Enrollment.EnrollTotpAsync();
    }

    [Test]
    public async Task LogIn_WithAuthenticatorCode_IsSuccessful()
    {
        //Arrange
        var twoFactorPage = TwoFactorPage;

        //Act
        await LoginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);
        await twoFactorPage.SubmitCodeAsync(Enrollment.LoginTotpCode());

        //Assert
        await AssertVaultIsUnlockedAsync();
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
        await AssertCodeWasRejectedAsync();
    }
}
