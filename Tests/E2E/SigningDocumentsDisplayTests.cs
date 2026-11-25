using System.Data;
using System.Net;
using Playwright.Finexus.Api;
using Playwright.Finexus.Base;
using Playwright.Finexus.Helpers;
using Playwright.Finexus.Interfaces;
using Playwright.Finexus.Pages;

namespace Playwright.Finexus.Tests;

/// <summary>Validacion intermedia: el documento se muestra correctamente (sin OTP ni firma).</summary>
[TestClass]
[TestCategory("Signing")]
public class SigningDocumentsDisplayTests : TestBase
{
    private static readonly string SigningDataPath = Path.Combine(AppContext.BaseDirectory, "Data/SigningData.json");

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
    [Description("TC-FIRMA-DOC-01: 1_Autorización_Consultas (2 páginas)")]
    public Task Sign_WithValidPdf_ShouldDisplayDocument() => DisplayDocumentAsync("1_Autorización_Consultas.pdf", 2);

    [TestMethod]
    [Description("TC-FIRMA-DOC-02: 2_Solicitud_de_Crédito (4 páginas)")]
    public Task Display_SolicitudDeCredito() => DisplayDocumentAsync("2_Solicitud_de_Crédito.pdf", 4);

    [TestMethod]
    [Description("TC-FIRMA-DOC-03: 3_Conocimiento_del_Cliente (2 páginas)")]
    public Task Display_ConocimientoDelCliente() => DisplayDocumentAsync("3_Conocimiento_del_Cliente.pdf", 2);

    [TestMethod]
    [Description("TC-FIRMA-DOC-04: 5_Contrato_de_Mutuo (5 páginas)")]
    public Task Display_ContratoDeMutuo() => DisplayDocumentAsync("5_Contrato_de_Mutuo.pdf", 5);

    [TestMethod]
    [Description("TC-FIRMA-DOC-05: 6_Contrato_de_Fianza (2 páginas)")]
    public Task Display_ContratoDeFianza() => DisplayDocumentAsync("6_Contrato_de_Fianza.pdf", 2);

    [TestMethod]
    [Description("TC-FIRMA-DOC-06: 7_Autorización_de_descuento (1 página)")]
    public Task Display_AutorizacionDeDescuento() => DisplayDocumentAsync("7_Autorización_de_descuento.pdf", 1);

    [TestMethod]
    [Description("TC-FIRMA-DOC-07: 11_Boletin_condiciones_finales_de_Aprobación (2 páginas)")]
    public Task Display_BoletinCondicionesFinales() => DisplayDocumentAsync("11_Boletin_condiciones_finales_de_Aprobación.pdf", 2);

    [TestMethod]
    [Description("TC-FIRMA-DOC-08: 12_Autorización_fianza_FGA (4 páginas)")]
    public Task Display_AutorizacionFianzaFga() => DisplayDocumentAsync("12_Autorización_fianza_FGA.pdf", 4);

    [TestMethod]
    [Description("TC-FIRMA-DOC-09: 13_Plan_de_pagos (8 páginas)")]
    public Task Display_PlanDePagos() => DisplayDocumentAsync("13_Plan_de_pagos.pdf", 8);

    private async Task DisplayDocumentAsync(string fileName, int expectedPages)
    {
        // Arrange
        var errors = new List<string>();
        DataRow row = DataHelper.ReadJsonData(SigningDataPath, "signPdfValid").Rows[0];
        var request = new IniciarProcesoRequest
        {
            TipoIdentificacion = row["tipoIdentificacion"].ToString()!,
            Identificacion = row["identificacion"].ToString()!,
            Nombre = row["nombre"].ToString()!,
            SegundoNombre = row["segundoNombre"].ToString()!,
            Apellido = row["apellido"].ToString()!,
            SegundoApellido = row["segundoApellido"].ToString()!,
            CorreoElectronico = row["correoElectronico"].ToString()!,
            Telefono = row["telefono"].ToString()!
        };

        // Act
        var start = await ExtentStepAsync("Iniciar proceso", () => _processClient.StartAsync(request));
        if (start.Status != HttpStatusCode.Created) errors.Add($"Iniciar: expected 201, got {(int)start.Status}.");
        var processSerial = start.Body?.Data?.ProcesoSerial;
        if (string.IsNullOrWhiteSpace(processSerial))
        {
            Assert.Fail(string.Join(Environment.NewLine, errors.Append("procesoSerial is empty.")));
        }

        var content = await FileHelper.ToBase64Async(FileHelper.GetTestFilePath(fileName));
        var issue = await ExtentStepAsync("Emitir documento", () => _documentClient.IssueAsync(new EmitirDocumentoRequest
        {
            ProcesoSerial = processSerial!,
            Documentos = { new DocumentoItem { NombreArchivo = fileName, ContenidoBase64 = content } }
        }));
        if (issue.Status != HttpStatusCode.OK) errors.Add($"Emitir: expected 200, got {(int)issue.Status}.");
        var signingUrl = issue.Body?.Data?.Url;
        if (string.IsNullOrWhiteSpace(signingUrl))
        {
            Assert.Fail(string.Join(Environment.NewLine, errors.Append("url is empty.")));
        }

        var failedRequests = new List<string>();
        Page.RequestFailed += (_, r) => failedRequests.Add($"{r.Url} -> {r.Failure}");

        var welcomePage = new WelcomePage(Page, signingUrl!);
        var documentsPage = new DocumentsPage(Page, signingUrl!);

        await ExtentStepAsync("Abrir bienvenida", welcomePage.NavigateAsync);
        await ExtentStepAsync("Continuar bienvenida", welcomePage.ContinueAsync);
        await ExtentStepAsync("Esperar pantalla de documentos", documentsPage.NavigateAsync);
        var viewerError = await ExtentStepAsync("Esperar visor PDF", () => documentsPage.WaitForViewerResultAsync());

        // Assert
        await ExtentStepAsync("Validar documento mostrado", async () =>
        {
            if (!await documentsPage.IsDocumentListedAsync(fileName)) errors.Add($"Documento '{fileName}' no aparece en la lista.");
            if (viewerError is not null)
            {
                var screenshot = await documentsPage.TakeScreenshotAsync("DocumentViewerError");
                errors.Add($"Visor PDF: {viewerError}. Peticiones fallidas: {string.Join(" | ", failedRequests)}. Screenshot: {Path.GetFileName(screenshot)}");
            }

            var pageCount = await documentsPage.GetPageCountAsync();
            if (pageCount != expectedPages) errors.Add($"Paginas del documento: expected {expectedPages}, got {pageCount}.");

            Assert.AreEqual(0, errors.Count, string.Join(Environment.NewLine, errors));
        });
    }
}
