using System.Text.RegularExpressions;
using e2e.Web.Pages;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests;

public class AuthenticationTests : BaseTest
{
    private LoginPage _loginPage;

    [SetUp]
    public void SetUp()
    {
        _loginPage = new LoginPage(Page);
    }

    [Test]
    public async Task LogIn_WithValidCredentials_IsSuccessful()
    {
        //Arrange
        var vault = new VaultPage(Page);

        //Act
        await _loginPage.Form.SubmitAsync(Account.Email, Account.Password);

        //Assert
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/vault(?:[/?]|$)"));
        await Expect(vault.ItemsTable).ToBeVisibleAsync();
        await Expect(vault.MasterPassword).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task LogIn_WithInvalidCredentials_Fails()
    {
        //Arrange
        var invalidPassword = Account.Password + "-incorrect";

        //Act
        await _loginPage.Form.SubmitAsync(Account.Email, invalidPassword);

        //Assert
        await Expect(_loginPage.Form.InvalidPassword).ToBeVisibleAsync();
        await Expect(_loginPage.Form.MasterPassword).ToBeVisibleAsync();
        await Expect(_loginPage.Form.LogInButton).ToBeEnabledAsync();
        await Expect(Page).ToHaveUrlAsync(new Regex(@"/#/login(?:[?]|$)"));
    }
}
