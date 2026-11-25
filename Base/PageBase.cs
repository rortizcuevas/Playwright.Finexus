using Microsoft.Playwright;
using Playwright.Finexus.Interfaces;

namespace Playwright.Finexus.Base;

public abstract class PageBase : IPageBase
{
    protected PageBase(IPage page, string baseUrl)
    {
        Page = page;
        BaseUrl = baseUrl;
    }

    protected IPage Page { get; }
    protected string BaseUrl { get; }

    public virtual async Task NavigateAsync()
        => await Page.GotoAsync(BaseUrl);

    public async Task WaitForSelectorAsync(string selector, float timeout = 30000)
        => await Page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = timeout });

    public async Task<string> TakeScreenshotAsync(string name)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
        return path;
    }
}
