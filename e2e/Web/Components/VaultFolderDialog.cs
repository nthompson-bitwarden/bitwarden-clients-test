using Dramaturge;

namespace e2e.Web.Components;

public sealed class VaultFolderDialog(Page page)
{
    public ElementLocator Root => page.GetByRole("dialog");
    public async Task CreateAsync(string name)
    {
        await Root.GetByLabel("Folder name").FillAsync(name);
        await Root.GetByRole("button", "Save").ClickAsync();
        await Root.WaitForAsync(ElementState.Hidden);
    }
}
