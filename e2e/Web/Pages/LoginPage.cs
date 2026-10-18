using Dramaturge;
using e2e.Web.Components;

namespace e2e.Web.Pages;

public sealed class LoginPage(Page page)
{
    public LoginForm LoginFormComponent { get; } = new(page);
}
