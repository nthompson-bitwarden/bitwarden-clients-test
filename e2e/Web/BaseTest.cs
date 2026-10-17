using Dramaturge;
using e2e.Infrastructure;

namespace e2e.Web;

public abstract class BaseTest : e2e.BaseTest
{
    protected override ClientKind Client => ClientKind.Web;

    protected override async Task<Page> OpenPageAsync()
    {
        var page = await Group.DefaultBrowser.NewPageAsync();
        await page.NavigateAsync(TestRuntime.Shared.WebUrl);
        return page;
    }
}
