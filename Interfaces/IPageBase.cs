namespace Playwright.Finexus.Interfaces;

public interface IPageBase
{
    Task NavigateAsync();
    Task WaitForSelectorAsync(string selector, float timeout = 30000);
    Task<string> TakeScreenshotAsync(string name);
}
