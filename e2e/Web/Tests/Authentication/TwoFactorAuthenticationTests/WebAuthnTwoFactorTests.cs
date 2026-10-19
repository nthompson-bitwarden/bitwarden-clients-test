using System.Text.RegularExpressions;
using Dramaturge.Browsers;
using NUnit.Framework;
using e2e.Authentication;
using e2e.Web.Pages;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Authentication;

public sealed class WebAuthnTwoFactorTests : BaseTest
{
    private TwoFactorEnrollment Enrollment = null!;
    private LoginPage LoginPage = null!;
    private VaultPage VaultPage = null!;

    [SetUp]
    public async Task PrepareFactorAsync()
    {
        LoginPage = new LoginPage(Page);
        VaultPage = new VaultPage(Page);
        Enrollment = new TwoFactorEnrollment(ServerUrl, Seeds);
        await Enrollment.AuthenticateAsync();
    }

    private VirtualAuthenticator? authenticator;

    [TearDown]
    public async Task RemoveAuthenticatorAsync()
    {
        try
        {
            if (authenticator is not null) await authenticator.DisposeAsync();
        }
        finally
        {
            Enrollment?.Dispose();
        }
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
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/vault(?:[/?]|$)"));
        await Expect(VaultPage.ItemsTable).ToBeVisibleAsync();
        await Expect(Page.GetByText("E2E login", exact: true)).ToBeVisibleAsync();
        await Expect(VaultPage.MasterPassword).Not.ToBeVisibleAsync();
        Assert.That(await authenticator.GetSignatureCountAsync(), Is.GreaterThan(signatureCount));
    }
}
