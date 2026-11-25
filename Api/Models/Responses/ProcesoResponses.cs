namespace Playwright.Finexus.Api;

public class IniciarProcesoResponse
{
    public string ProcesoSerial { get; set; } = string.Empty;
}

public class EmitirDocumentoResponse
{
    public string TransaccionSerial { get; set; } = string.Empty;
    public string? Url { get; set; }
}
