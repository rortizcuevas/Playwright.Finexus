using Microsoft.Playwright;
using Playwright.Finexus.Base;

namespace Playwright.Finexus.Pages;

public class SigningCompletedPage : PageBase
{
    private const string CompletedTitle_Heading = "h1:has-text('Proceso finalizado')";
    private const string SignedMessage_Text = "p:has-text('Tus documentos ya fueron firmados y enviados.')";

    public SigningCompletedPage(IPage page, string signingUrl) : base(page, signingUrl)
    {
    }

    private ILocator CompletedTitle => Page.Locator(CompletedTitle_Heading);
    private ILocator SignedMessage => Page.Locator(SignedMessage_Text);

    public override async Task NavigateAsync()
        => await CompletedTitle.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

    public async Task<bool> IsDisplayedAsync()
        => await CompletedTitle.First.IsVisibleAsync() && await SignedMessage.First.IsVisibleAsync();
}
