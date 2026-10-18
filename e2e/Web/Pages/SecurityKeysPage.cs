using Dramaturge;
using WebDriverBiDi.BrowsingContext;

namespace e2e.Web.Pages;

public sealed class SecurityKeysPage(Page page)
{
    public ElementLocator EncryptionSettings => page.GetByRole("heading", "Encryption key settings");
    public ElementLocator ApiKeyHeading => page.GetByRole("heading", "API Key");
    public ElementLocator Algorithm => page.GetByRole("combobox");
    public ElementLocator SelectedAlgorithm => page.GetByTestId("bit-select-value");
    public ElementLocator Iterations => page.GetByLabel("KDF iterations");
    public ElementLocator Memory => page.GetByLabel("KDF memory (MB)");
    public ElementLocator Parallelism => page.GetByLabel("KDF parallelism");
    public ElementLocator Saved => page.GetByText("Encryption key settings saved");

    public async Task OpenAsync()
    {
        // The avatar trigger has no accessible name; scope it to the account-menu component.
        await page.Locate(new CssLocator("app-account-menu button")).ClickAsync();
        await page.GetByText("Account settings").ClickAsync();
        await page.GetByRole("link", "Security").ClickAsync();
        await page.GetByRole("link", "Keys").ClickAsync();
        await EncryptionSettings.WaitForAsync();
        await Iterations.WaitForAsync();
    }

    public Task ViewApiKeyAsync() => page.GetByRole("button", "View API key").ClickAsync();
    public Task RotateApiKeyAsync() => page.GetByRole("button", "Rotate API key").ClickAsync();
    public Task UpdateAsync() => page.GetByRole("button", "Update encryption settings").ClickAsync();

    public async Task SelectAlgorithmAsync(string algorithm)
    {
        await Algorithm.ClickAsync();
        await page.GetByRole("option", algorithm).ClickAsync();
    }
}
