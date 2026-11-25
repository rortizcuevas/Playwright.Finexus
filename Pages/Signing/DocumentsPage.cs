using Microsoft.Playwright;
using Playwright.Finexus.Base;

namespace Playwright.Finexus.Pages;

public class DocumentsPage : PageBase
{
    private const string Title_Heading = "h1:has-text('documentos')";
    private const string PdfError_Text = "text=No se pudo cargar el PDF";

    public DocumentsPage(IPage page, string signingUrl) : base(page, signingUrl)
    {
    }

    private ILocator Title => Page.Locator(Title_Heading).First;
    private ILocator PdfError => Page.Locator(PdfError_Text).First;
    private ILocator PdfCanvas => Page.Locator(".page canvas, [role='region'][aria-label^='Page'] canvas").First;

    public override async Task NavigateAsync()
        => await Title.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

    public async Task<bool> IsDocumentListedAsync(string fileName)
    {
        var normalized = Normalize(Path.GetFileNameWithoutExtension(fileName));
        var bodyText = Normalize(await Page.Locator("body").InnerTextAsync());
        return bodyText.Contains(normalized);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c == '_' || c == ' ' ? '-' : char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    public async Task<int> GetPageCountAsync()
        => await Page.Locator("[role='region'][aria-label^='Page']").CountAsync();

    /// <summary>Espera a que el visor
    public async Task<string?> WaitForViewerResultAsync(float timeoutMs = 20000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await PdfError.IsVisibleAsync()) return (await PdfError.InnerTextAsync()).Trim();
            if (await PdfCanvas.IsVisibleAsync()) return null;
            await Task.Delay(500);
        }
        return "El visor no mostro el PDF ni un error dentro del tiempo esperado.";
    }
}
