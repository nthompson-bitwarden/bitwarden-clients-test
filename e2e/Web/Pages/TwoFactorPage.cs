using Dramaturge;

namespace e2e.Web.Pages;

public sealed class TwoFactorPage(Page page)
{
    public ElementLocator VerificationCode => page.GetByLabel("Verification code");
    public ElementLocator ContinueButton => page.GetByRole("button", "Continue logging in");
    public ElementLocator InvalidCode => page.GetByText("Invalid verification code", exact: true);

    public async Task SubmitCodeAsync(string code)
    {
        await VerificationCode.FillAsync(code);
        await ContinueButton.ClickAsync();
    }
}
