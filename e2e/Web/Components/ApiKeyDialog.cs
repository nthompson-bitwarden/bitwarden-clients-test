using Dramaturge;
using WebDriverBiDi.BrowsingContext;

namespace e2e.Web.Components;

public sealed class ApiKeyDialog(Page page)
{
    public ElementLocator Root => page.GetByRole("dialog");
    public ElementLocator MasterPassword => Root.GetByLabel("Master password");
    // Credential values are plain code elements without roles or labels. Scope by their visible field heading.
    public ElementLocator ClientId => Credential("client_id:");
    public ElementLocator ClientSecret => Credential("client_secret:");
    public ElementLocator Scope => Credential("scope:");
    public ElementLocator GrantType => Credential("grant_type:");

    private ElementLocator Credential(string heading) => Root.Locate(new CssLocator("p"))
        .Filter(has: page.GetByText(heading, exact: true)).Locate(new CssLocator("code"));

    public async Task VerifyAsync(string password, bool rotate = false)
    {
        await MasterPassword.FillAsync(password);
        await Root.GetByRole("button", rotate ? "Rotate API key" : "View API key").ClickAsync();
        await ClientSecret.WaitForAsync();
    }

    public async Task CloseAsync()
    {
        await Root.GetByRole("button", "Close").Filter(hasText: "Close").ClickAsync();
        await Root.WaitForAsync(ElementState.Hidden);
    }
}
