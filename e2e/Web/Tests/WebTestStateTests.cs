using Dramaturge.NUnit;
using e2e.Infrastructure;
using NUnit.Framework;
using static Dramaturge.Assertions;

namespace e2e.Web.Tests;

[NonParallelizable]
public sealed class WebTestStateTests : PageTest
{
    [TestCase("https://vault.e2e.test", "dismissed")]
    [TestCase("https://other.e2e.test", "fresh")]
    public async Task OnboardingStateIsAvailableBeforeAppScriptsOnlyOnWebOrigin(string origin, string expected)
    {
        //Arrange
        var account = new SeededAccount("test@example.com", "unused", "11111111-1111-1111-1111-111111111111");
        await Page.AddInitScriptAsync(WebTestState.CreateInitScript("https://vault.e2e.test", account));
        await Page.RouteAsync(origin + "/", route => route.FulfillAsync(200, """
            <output role="status"></output>
            <script>
                const prefix = 'user_11111111-1111-1111-1111-111111111111_';
                const dismissed = JSON.parse(localStorage.getItem(prefix + 'setupExtensionDismissed_setupExtensionDismissed'));
                const tasks = JSON.parse(localStorage.getItem(prefix + 'vaultOnboarding_tasks'));
                document.querySelector('output').textContent =
                    dismissed && tasks?.createAccount && tasks?.importData && tasks?.installExtension
                        ? 'dismissed' : 'fresh';
            </script>
            """, new Dictionary<string, string> { ["Content-Type"] = "text/html" }));

        //Act
        await Page.NavigateAsync(origin + "/");

        //Assert
        await Expect(Page.GetByRole("status")).ToHaveTextAsync(expected);
    }
}
