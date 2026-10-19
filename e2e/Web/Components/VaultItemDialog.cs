using Dramaturge;
using WebDriverBiDi.BrowsingContext;

namespace e2e.Web.Components;

public sealed class VaultItemDialog(Page page)
{
    public ElementLocator Root => page.GetByRole("dialog");
    public ElementLocator Name => Root.GetByLabel("Item name");
    public ElementLocator Field(string label) => Root.GetByLabel(label, exact: true);
    // Labels resolve to ng-select search inputs; selected text is exposed separately.
    public ElementLocator SelectedFolder => Root.Locate(new CssLocator("bit-select[formcontrolname=folderId]")).GetByTestId("bit-select-value");
    public ElementLocator SelectedExpirationMonth => Root.Locate(new CssLocator("bit-select[formcontrolname=expMonth]")).GetByTestId("bit-select-value");

    public ElementLocator EditTitle(string type) => Root.GetByText(
        "Edit " + (type == "Secure note" ? "note" : type.ToLowerInvariant()), exact: true);

    public async Task FillFieldsAsync(IReadOnlyDictionary<string, string> fields, string? expirationMonth = null)
    {
        foreach (var field in fields)
            await FillAsync(field.Key, field.Value);
        if (expirationMonth is not null)
            await SelectAsync("Expiration month", expirationMonth);
    }

    public async Task<bool> FieldsMatchAsync(IReadOnlyDictionary<string, string> fields, string? expirationMonth = null)
    {
        var fieldsMatch = true;
        foreach (var field in fields)
            fieldsMatch &= await Field(field.Key).InputValueAsync() == field.Value;
        if (expirationMonth is not null)
            fieldsMatch &= (await SelectedExpirationMonth.TextContentAsync()).Trim() == expirationMonth;
        return fieldsMatch;
    }

    public async Task<bool> SelectedFolderMatchesAsync(string folder) =>
        (await SelectedFolder.TextContentAsync()).Trim() == folder;

    public async Task<bool> ReadOnlyLoginMatchesAsync(string username, string website) =>
        await Root.GetByTestId("login-username").InputValueAsync() == username &&
        await Root.GetByTestId("login-website").InputValueAsync() == website;

    public async Task FillAsync(string label, string value)
    {
        // Dramaturge 0.0.2 cannot scroll the long Identity form inside this modal.
        // Scroll the real control into its native container before the normal fill action.
        await Field(label).EvaluateAsync("element => element.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await Field(label).FillAsync(value);
    }

    public async Task SelectAsync(string label, string option)
    {
        await Field(label).ClickAsync();
        await page.GetByRole("option", option).ClickAsync();
    }

    public async Task CancelAsync()
    {
        await Root.GetByRole("button", "Cancel").ClickAsync();
        // Canceling an edit also returns to the existing read-only view.
        await Root.GetByRole("button", "Edit").WaitForAsync();
        await Root.GetByRole("button", "Close").ClickAsync();
        await Root.WaitForAsync(ElementState.Hidden);
    }

    public async Task SaveAsync()
    {
        await Root.GetByRole("button", "Save").ClickAsync();
        // Saving returns to the read-only item view in this Web experience.
        await Root.GetByRole("button", "Edit").WaitForAsync();
        await Root.GetByRole("button", "Close").ClickAsync();
        await Root.WaitForAsync(ElementState.Hidden);
    }
}
