using Microsoft.Playwright;
using Playwright.Finexus.Base;

namespace Playwright.Finexus.Pages;

public class WelcomePage : PageBase
{
    private const string ContinueButton_Button = "button:has-text('Continuar')";

    public WelcomePage(IPage page, string signingUrl) : base(page, signingUrl)
    {
    }

    private ILocator ContinueButton => Page.Locator(ContinueButton_Button);

    public override async Task NavigateAsync()
    {
        await base.NavigateAsync();
        await ContinueButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    public async Task ContinueAsync() => await ContinueButton.ClickAsync();
}
