using e2e.Web.Pages;
using NUnit.Framework;

namespace e2e.Web.Tests.Onboarding;

public abstract class OnboardingTests : BaseTest
{
    private LoginPage _loginPage = null!;
    protected VaultPage Vault = null!;
    private protected OnboardingScenario Scenario = null!;

    protected override Task ConfigureOnboardingAsync() => Task.CompletedTask;
    protected abstract Task PrepareScenarioAsync();

    [SetUp]
    public async Task SetUp()
    {
        _loginPage = new LoginPage(Page);
        Vault = new VaultPage(Page);
        Scenario = new OnboardingScenario(Page, ServerUrl, Account);
        await PrepareScenarioAsync();
        await _loginPage.Form.SubmitAsync(Account.Email, Account.Password);
    }

    [TearDown]
    public void TearDown() => Scenario?.Dispose();
}
