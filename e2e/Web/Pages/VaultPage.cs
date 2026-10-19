using Dramaturge;
using e2e.Web.Components;
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

    public ElementLocator Search => page.GetByLabel("Search", exact: true);
    public ElementLocator Item(string name) => ItemsTable.GetByRole("button", name);
    public ElementLocator Row(string name) => ItemsTable.GetByRole("row").Filter(has: page.GetByRole("button", name));

    public async Task NewItemAsync(string type)
    {
        await page.GetByRole("button", "Add").ClickAsync();
        await page.GetByRole("menuitem", type == "Secure note" ? "Note" : type).ClickAsync();
        await ItemDialog.WaitForAsync();
    }

    public async Task ItemActionAsync(string name, string action)
    {
        await Row(name).GetByRole("button", "More options").ClickAsync();
        // Chromium exposes leading spaces in these icon-bearing menu item names.
        await page.GetByRole("menu").GetByText(action).ClickAsync();
        if (action == "Favorite" || action == "Unfavorite")
            await page.GetByText(action == "Favorite" ? "Item added to favorites" : "Item removed from favorites", exact: true).WaitForAsync();
    }

    public async Task EditItemAsync(string name)
    {
        await ItemActionAsync(name, "Edit");
        await new VaultItemDialog(page).Name.WaitForAsync();
    }

    public async Task DeleteItemAsync(string name)
    {
        await ItemActionAsync(name, "Delete");
        await ConfirmAsync("Yes");
        await Item(name).WaitForAsync(ElementState.Hidden);
    }

    public async Task ConfirmAsync(string action)
    {
        var dialog = page.GetByRole("dialog");
        await dialog.GetByRole("button", action).ClickAsync();
        await dialog.WaitForAsync(ElementState.Hidden);
    }

    public Task FilterAsync(string name) => page.GetByRole("button", "Filter: " + name).ClickAsync();
    public Task SelectAsync(string name) => Row(name).GetByRole("checkbox").CheckAsync();
    public Task BulkDeleteAsync() => page.GetByRole("button", "Delete").ClickAsync();

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

    public ElementLocator EmptyTrash => page.GetByText("No items in trash", exact: true);
    public ElementLocator EmptyFavorites => page.GetByText("You haven't favorited any items", exact: true);

    public async Task ReloadReadyAsync()
    {
        await page.ReloadAsync();
        await ItemsTable.WaitForAsync();
        // The table renders before the initial decrypted list is ready.
        await page.GetByTitle("Loading", exact: true).WaitForAsync(ElementState.Hidden);
        await page.GetByRole("button", "Filter: All items").WaitForAsync();
    }

    public async Task ReloadAsync()
    {
        await page.ReloadAsync();
        await ItemsTable.WaitForAsync();
    }
}
