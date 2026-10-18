using System.Text.RegularExpressions;
using e2e.Web.Pages;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests;

public class BasicAuthenticationTests : BaseTest
{
    private LoginPage _loginPage = null!;

    [SetUp]
    public void SetUp()
    {
        _loginPage = new LoginPage(Page);
    }

    [Test]
    public async Task LogIn_WithValidCredentials_IsSuccessful()
    {
        //Arrange
        var vaultPage = new VaultPage(Page);

        //Act
        await _loginPage.LoginFormComponent.SubmitAsync(Account.Email, Account.Password);

        //Assert
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/vault(?:[/?]|$)"));
        await Expect(vaultPage.ItemsTable).ToBeVisibleAsync();
        await Expect(vaultPage.MasterPassword).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task LogIn_WithInvalidCredentials_Fails()
    {
        //Arrange
        var invalidPassword = Account.Password + "-incorrect";

        //Act
        await _loginPage.LoginFormComponent.SubmitAsync(Account.Email, invalidPassword);

        //Assert
        await Expect(_loginPage.LoginFormComponent.InvalidPassword).ToBeVisibleAsync();
        await Expect(_loginPage.LoginFormComponent.MasterPassword).ToBeVisibleAsync();
        await Expect(_loginPage.LoginFormComponent.LogInButton).ToBeEnabledAsync();
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/login(?:[?]|$)"));
    }
}
