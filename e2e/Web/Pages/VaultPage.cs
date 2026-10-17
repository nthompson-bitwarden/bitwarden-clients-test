using Dramaturge;

namespace e2e.Web.Pages;

public sealed class VaultPage(Page page)
{
    public ElementLocator ItemsTable => page.GetByRole("table");
    public ElementLocator MasterPassword => page.GetByLabel("Master password");
}
