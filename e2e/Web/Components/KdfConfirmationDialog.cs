using Dramaturge;

namespace e2e.Web.Components;

public sealed class KdfConfirmationDialog(Page page)
{
    public ElementLocator Root => page.GetByRole("dialog");

    public async Task ConfirmAsync(string password)
    {
        await Root.GetByLabel("Master password").FillAsync(password);
        await Root.GetByRole("button", "Update settings").ClickAsync();
        await Root.WaitForAsync(ElementState.Hidden);
    }
}
