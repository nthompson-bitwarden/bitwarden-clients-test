using System.Text.RegularExpressions;
using e2e.Web.Pages;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests.Authentication;

public abstract class TwoFactorAuthenticationTests : BaseTest
{
    private protected TwoFactorEnrollment Enrollment = null!;
    protected LoginPage Login = null!;
    protected TwoFactorPage Challenge = null!;
    protected VaultPage Vault = null!;

    [SetUp]
    public async Task PrepareFactorAsync()
    {
        Login = new LoginPage(Page);
        Challenge = new TwoFactorPage(Page);
        Vault = new VaultPage(Page);
        Enrollment = new TwoFactorEnrollment(ServerUrl, Seeds);
        await Enrollment.AuthenticateAsync();
    }

    [TearDown]
    public void DisposeEnrollment() => Enrollment?.Dispose();

    protected async Task AssertVaultIsUnlockedAsync()
    {
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/vault(?:[/?]|$)"));
        await Expect(Vault.ItemsTable).ToBeVisibleAsync();
        await Expect(Page.GetByText("E2E login", exact: true)).ToBeVisibleAsync();
        await Expect(Vault.MasterPassword).Not.ToBeVisibleAsync();
    }

    protected async Task AssertCodeWasRejectedAsync()
    {
        await Expect(Challenge.InvalidCode).ToBeVisibleAsync();
        await Expect(Challenge.VerificationCode).ToBeVisibleAsync();
        await Expect(Challenge.ContinueButton).ToBeEnabledAsync();
        await Expect(Vault.ItemsTable).Not.ToBeVisibleAsync();
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/2fa(?:[/?]|$)"));
    }
}
