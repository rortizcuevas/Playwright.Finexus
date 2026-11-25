namespace Playwright.Finexus.Api;

public class EmitirDocumentoRequest
{
    public string ProcesoSerial { get; set; } = string.Empty;
    public List<DocumentoItem> Documentos { get; set; } = new();
}

public class DocumentoItem
{
    public string NombreArchivo { get; set; } = string.Empty;
    public string ContenidoBase64 { get; set; } = string.Empty;
}
