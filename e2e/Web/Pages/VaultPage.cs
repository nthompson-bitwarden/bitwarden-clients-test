using Dramaturge;
using WebDriverBiDi.BrowsingContext;

namespace e2e.Web.Pages;

public sealed class VaultPage(Page page)
{
    public ElementLocator ItemsTable => page.GetByRole("table");
    public ElementLocator MasterPassword => page.GetByLabel("Master password");

    public ElementLocator SeededLogin => ItemsTable.GetByRole("button", "E2E login");
    public ElementLocator ItemDialog => page.GetByRole("dialog");
    // Label substring lookup also matches the copy/launch buttons; use the dedicated field IDs.
    public ElementLocator LoginUsername => ItemDialog.GetByTestId("login-username");
    public ElementLocator LoginWebsite => ItemDialog.GetByTestId("login-website");
    public ElementLocator LoginFolder => ItemDialog.GetByText("E2E folder");

    public async Task LogOutAsync()
    {
        // The avatar trigger has no accessible name; scope it to the account-menu component.
        await page.Locate(new CssLocator("app-account-menu button")).ClickAsync();
        await page.GetByText("Log out").ClickAsync();
        await page.WaitForUrlAsync(new System.Text.RegularExpressions.Regex(@"/#/login(?:[?]|$)"));
    }

    public async Task OpenSeededLoginAsync()
    {
        await SeededLogin.ClickAsync();
        await LoginUsername.WaitForAsync();
    }

    public async Task CloseItemAsync()
    {
        await ItemDialog.GetByRole("button", "Close").ClickAsync();
        await ItemDialog.WaitForAsync(ElementState.Hidden);
    }

    public async Task ReloadAsync()
    {
        await page.ReloadAsync();
        await ItemsTable.WaitForAsync();
    }
}
