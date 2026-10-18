using e2e.Authentication;
using System.Text.RegularExpressions;
using e2e.Web.Pages;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Authentication;

public abstract class TwoFactorAuthenticationTests : BaseTest
{
    private protected TwoFactorEnrollment Enrollment = null!;
    protected LoginPage LoginPage = null!;
    protected TwoFactorPage TwoFactorPage = null!;
    protected VaultPage VaultPage = null!;

    [SetUp]
    public async Task PrepareFactorAsync()
    {
        LoginPage = new LoginPage(Page);
        TwoFactorPage = new TwoFactorPage(Page);
        VaultPage = new VaultPage(Page);
        Enrollment = new TwoFactorEnrollment(ServerUrl, Seeds);
        await Enrollment.AuthenticateAsync();
    }

    [TearDown]
    public void DisposeEnrollment() => Enrollment?.Dispose();

    protected async Task AssertVaultIsUnlockedAsync()
    {
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/vault(?:[/?]|$)"));
        await Expect(VaultPage.ItemsTable).ToBeVisibleAsync();
        await Expect(Page.GetByText("E2E login", exact: true)).ToBeVisibleAsync();
        await Expect(VaultPage.MasterPassword).Not.ToBeVisibleAsync();
    }

    protected async Task AssertCodeWasRejectedAsync()
    {
        await Expect(TwoFactorPage.InvalidCode).ToBeVisibleAsync();
        await Expect(TwoFactorPage.VerificationCode).ToBeVisibleAsync();
        await Expect(TwoFactorPage.ContinueButton).ToBeEnabledAsync();
        await Expect(VaultPage.ItemsTable).Not.ToBeVisibleAsync();
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/2fa(?:[/?]|$)"));
    }
}
