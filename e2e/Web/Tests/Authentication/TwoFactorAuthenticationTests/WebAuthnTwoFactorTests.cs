using e2e.Authentication;
using Dramaturge.Browsers;
using NUnit.Framework;

namespace e2e.Web.Tests.Authentication;

public sealed class WebAuthnTwoFactorTests : TwoFactorAuthenticationTests
{
    private VirtualAuthenticator? authenticator;

    [TearDown]
    public async Task RemoveAuthenticatorAsync()
    {
        if (authenticator is not null) await authenticator.DisposeAsync();
    }

    [Test]
    public async Task LogIn_WithWebAuthnPasskey_IsSuccessful()
    {
        //Arrange
        if (SetupFixture.Settings.Browser is not (BrowserKind.Chrome or BrowserKind.Edge))
            Assert.Ignore("This test's virtual authenticator uses Chromium-specific CDP commands.");
        authenticator = new VirtualAuthenticator(Page);
        await authenticator.InitializeAsync();
        await Enrollment.EnrollWebAuthnAsync(Page);
        var signatureCount = await authenticator.GetSignatureCountAsync();
        var loginFormComponent = LoginPage.LoginFormComponent;

        //Act
        await loginFormComponent.SubmitAsync(Account.Email, Account.Password);

        //Assert
        await AssertVaultIsUnlockedAsync();
        Assert.That(await authenticator.GetSignatureCountAsync(), Is.GreaterThan(signatureCount));
    }
}
