using System.Net;
using Playwright.Finexus.Api;
using Playwright.Finexus.Base;
using Playwright.Finexus.Helpers;
using Playwright.Finexus.Interfaces;

namespace Playwright.Finexus.Tests;

/// <summary>Emision de un PDF de mas de 5 MB: reporta si la API lo procesa o lo rechaza.</summary>
[TestClass]
[TestCategory("Document")]
public class DocumentIssueLargeFileTests : ApiTestBase
{
    private const string LargeFileName = "1_Autorización_Consultas.pdf";
    private const long FiveMegabytes = 5L * 1024 * 1024;

    private IProcessClient _processClient = null!;
    private IDocumentClient _documentClient = null!;

    protected override async Task SetupAsync()
    {
        await base.SetupAsync();
        var token = await TokenProvider.GetTokenAsync(new AuthClient(WebApiUrl), ApiUser, ApiPassword, ApiClientCode);
        _processClient = new ProcessClient(WebApiUrl, token);
        _documentClient = new DocumentClient(WebApiUrl, token);
    }

    [TestMethod]
    [Description("TC-DOCUMENT-06: Carga de PDF mayor a 5 MB")]
    public async Task Issue_WithFileLargerThan5Mb_ReportsResult()
    {
        var path = FileHelper.GetTestFilePath(LargeFileName);
        var size = new FileInfo(path).Length;
        Assert.IsTrue(size > FiveMegabytes, $"El archivo debe superar 5 MB; mide {size} bytes.");

        var startRequest = new IniciarProcesoRequest
        {
            TipoIdentificacion = "CC",
            Identificacion = "1234567890",
            Nombre = "Prueba",
            SegundoNombre = "Tamano",
            Apellido = "Archivo",
            SegundoApellido = "Grande",
            CorreoElectronico = "prueba.archivo@example.com",
            Telefono = "3001234567"
        };

        var start = await ExtentStepAsync("Iniciar proceso", () => _processClient.StartAsync(startRequest));
        Assert.AreEqual(HttpStatusCode.Created, start.Status, "Iniciar proceso fallo.");
        var processSerial = start.Body?.Data?.ProcesoSerial;
        Assert.IsFalse(string.IsNullOrWhiteSpace(processSerial), "procesoSerial vacio.");

        var content = await FileHelper.ToBase64Async(path);
        var issueRequest = new EmitirDocumentoRequest { ProcesoSerial = processSerial! };
        issueRequest.Documentos.Add(new DocumentoItem { NombreArchivo = LargeFileName, ContenidoBase64 = content });

        var issue = await ExtentStepAsync("Emitir documento mayor a 5 MB", () => _documentClient.IssueAsync(issueRequest));

        TestContext.WriteLine($"Tamano binario: {size} bytes; Base64: {content.Length} chars");
        TestContext.WriteLine($"Emitir status: {(int)issue.Status}; Success: {issue.Body?.Success}; Url: {issue.Body?.Data?.Url}");
        var accepted = issue.Body?.Success == true && issue.Status == HttpStatusCode.OK;
        Assert.IsTrue(accepted, $"La API RECHAZO {size} bytes y debia aceptarlo (status {(int)issue.Status}, mensaje: {issue.Body?.Message}).");
    }

    [TestMethod]
    [DataRow(12, DisplayName = "PDF 12 MB - supera el limite de 10 MB")]
    [Description("TC-DOCUMENT-08: Carga de PDF de 12 MB (supera el limite de 10 MB)")]
    public async Task Issue_WithGeneratedPdf_ReportsResult(int megabytes)
    {
        var bytes = PdfGenerator.CreatePdf(megabytes * 1024L * 1024L);
        var size = bytes.LongLength;

        var startRequest = new IniciarProcesoRequest
        {
            TipoIdentificacion = "CC",
            Identificacion = "1234567890",
            Nombre = "Prueba",
            SegundoNombre = "Tamano",
            Apellido = "Archivo",
            SegundoApellido = "Grande",
            CorreoElectronico = "prueba.archivo@example.com",
            Telefono = "3001234567"
        };

        var start = await ExtentStepAsync("Iniciar proceso", () => _processClient.StartAsync(startRequest));
        Assert.AreEqual(HttpStatusCode.Created, start.Status, "Iniciar proceso fallo.");
        var processSerial = start.Body?.Data?.ProcesoSerial;
        Assert.IsFalse(string.IsNullOrWhiteSpace(processSerial), "procesoSerial vacio.");

        var content = Convert.ToBase64String(bytes);
        var issueRequest = new EmitirDocumentoRequest { ProcesoSerial = processSerial! };
        issueRequest.Documentos.Add(new DocumentoItem { NombreArchivo = $"Generado{megabytes}mb.pdf", ContenidoBase64 = content });

        var issue = await ExtentStepAsync($"Emitir documento de {megabytes} MB", () => _documentClient.IssueAsync(issueRequest));

        TestContext.WriteLine($"Tamano binario: {size} bytes; Base64: {content.Length} chars");
        TestContext.WriteLine($"Emitir status: {(int)issue.Status}; Success: {issue.Body?.Success}; Mensaje: {issue.Body?.Message}; Url: {issue.Body?.Data?.Url}");
        var accepted = issue.Body?.Success == true && issue.Status == HttpStatusCode.OK;
        Assert.IsFalse(accepted, $"La API PERMITE procesar {size} bytes (status {(int)issue.Status}, mensaje: {issue.Body?.Message}, url: {issue.Body?.Data?.Url}).");
    }

    [TestMethod]
    [Description("TC-DOCUMENT-07: Carga de PDF de 10 MB")]
    public async Task Issue_WithFileAround10Mb_ReportsResult()
    {
        const string fileName = "no_permitidos/Documento10MB.pdf";
        var path = FileHelper.GetTestFilePath(fileName);
        var size = new FileInfo(path).Length;

        var startRequest = new IniciarProcesoRequest
        {
            TipoIdentificacion = "CC",
            Identificacion = "1234567890",
            Nombre = "Prueba",
            SegundoNombre = "Tamano",
            Apellido = "Archivo",
            SegundoApellido = "Grande",
            CorreoElectronico = "prueba.archivo@example.com",
            Telefono = "3001234567"
        };

        var start = await ExtentStepAsync("Iniciar proceso", () => _processClient.StartAsync(startRequest));
        Assert.AreEqual(HttpStatusCode.Created, start.Status, "Iniciar proceso fallo.");
        var processSerial = start.Body?.Data?.ProcesoSerial;
        Assert.IsFalse(string.IsNullOrWhiteSpace(processSerial), "procesoSerial vacio.");

        var content = await FileHelper.ToBase64Async(path);
        var issueRequest = new EmitirDocumentoRequest { ProcesoSerial = processSerial! };
        issueRequest.Documentos.Add(new DocumentoItem { NombreArchivo = "Documento10MB.pdf", ContenidoBase64 = content });

        var issue = await ExtentStepAsync("Emitir documento de 10 MB", () => _documentClient.IssueAsync(issueRequest));

        TestContext.WriteLine($"Tamano binario: {size} bytes; Base64: {content.Length} chars");
        TestContext.WriteLine($"Emitir status: {(int)issue.Status}; Success: {issue.Body?.Success}; Mensaje: {issue.Body?.Message}; Url: {issue.Body?.Data?.Url}");
        var accepted = issue.Body?.Success == true && issue.Status == HttpStatusCode.OK;
        Assert.IsTrue(accepted, $"La API RECHAZO {size} bytes y debia aceptarlo (status {(int)issue.Status}, mensaje: {issue.Body?.Message}).");
    }
}
