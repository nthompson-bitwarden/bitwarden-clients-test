using Dramaturge;
using WebDriverBiDi.BrowsingContext;

namespace e2e.Web.Components;

public sealed class LoginForm(Page page)
{
    // The routed login component has no selector or named form landmark.
    private ElementLocator Root => page.Locate(new CssLocator("form"))
        .Filter(has: page.GetByLabel("Email address"));
    public ElementLocator MasterPassword => Root.GetByLabel("Master password");
    public ElementLocator InvalidPassword => Root.GetByText("Invalid master password.");
    public ElementLocator LogInButton => Root.GetByRole("button", "Log in");

    public async Task SubmitAsync(string email, string password)
    {
        await Root.GetByLabel("Email address").FillAsync(email);
        await Root.GetByRole("button", "Continue").ClickAsync();
        await MasterPassword.FillAsync(password);
        await LogInButton.ClickAsync();
    }
}
