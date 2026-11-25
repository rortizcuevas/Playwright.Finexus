using System.Data;
using System.Net;
using Playwright.Finexus.Api;
using Playwright.Finexus.Base;
using Playwright.Finexus.Helpers;
using Playwright.Finexus.Interfaces;
using Playwright.Finexus.Pages;

namespace Playwright.Finexus.Tests;

/// <summary>Firma E2E completa para cada documento PDF disponible en TestFiles.</summary>
[TestClass]
[TestCategory("Signing")]
public class SigningAllDocumentsE2ETests : TestBase
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
    [Description("TC-FIRMA-DOC-SIGN-01: Firma 1_Autorización_Consultas")]
    public Task Sign_AutorizacionConsultas() => SignDocumentAsync("1_Autorización_Consultas.pdf");

    [TestMethod]
    [Description("TC-FIRMA-DOC-SIGN-02: Firma 2_Solicitud_de_Crédito")]
    public Task Sign_SolicitudDeCredito() => SignDocumentAsync("2_Solicitud_de_Crédito.pdf");

    [TestMethod]
    [Description("TC-FIRMA-DOC-SIGN-03: Firma 3_Conocimiento_del_Cliente")]
    public Task Sign_ConocimientoDelCliente() => SignDocumentAsync("3_Conocimiento_del_Cliente.pdf");

    [TestMethod]
    [Description("TC-FIRMA-DOC-SIGN-04: Firma 5_Contrato_de_Mutuo")]
    public Task Sign_ContratoDeMutuo() => SignDocumentAsync("5_Contrato_de_Mutuo.pdf");

    [TestMethod]
    [Description("TC-FIRMA-DOC-SIGN-05: Firma 6_Contrato_de_Fianza")]
    public Task Sign_ContratoDeFianza() => SignDocumentAsync("6_Contrato_de_Fianza.pdf");

    [TestMethod]
    [Description("TC-FIRMA-DOC-SIGN-06: Firma 7_Autorización_de_descuento")]
    public Task Sign_AutorizacionDeDescuento() => SignDocumentAsync("7_Autorización_de_descuento.pdf");

    [TestMethod]
    [Description("TC-FIRMA-DOC-SIGN-07: Firma 11_Boletin_condiciones_finales_de_Aprobación")]
    public Task Sign_BoletinCondicionesFinales() => SignDocumentAsync("11_Boletin_condiciones_finales_de_Aprobación.pdf");

    [TestMethod]
    [Description("TC-FIRMA-DOC-SIGN-08: Firma 12_Autorización_fianza_FGA")]
    public Task Sign_AutorizacionFianzaFga() => SignDocumentAsync("12_Autorización_fianza_FGA.pdf");

    [TestMethod]
    [Description("TC-FIRMA-DOC-SIGN-09: Firma 13_Plan_de_pagos")]
    public Task Sign_PlanDePagos() => SignDocumentAsync("13_Plan_de_pagos.pdf");

    private async Task SignDocumentAsync(string fileName)
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
        var start = await ExtentStepAsync("Act: iniciar proceso de firma por API", () => _processClient.StartAsync(request));
        if (start.Status != HttpStatusCode.Created) errors.Add($"Iniciar: expected 201, got {(int)start.Status}.");
        var processSerial = start.Body?.Data?.ProcesoSerial;
        if (string.IsNullOrWhiteSpace(processSerial))
        {
            Assert.Fail(string.Join(Environment.NewLine, errors.Append("procesoSerial is empty.")));
        }

        var content = await FileHelper.ToBase64Async(FileHelper.GetTestFilePath(fileName));
        var issue = await ExtentStepAsync("Act: emitir el documento PDF por API", () => _documentClient.IssueAsync(new EmitirDocumentoRequest
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

        var welcomePage = new WelcomePage(Page, signingUrl!);
        var readingPage = new ReadingPage(Page, signingUrl!);
        var otpPage = new OtpPage(Page, signingUrl!);

        await ExtentStepAsync("Act: abrir la pantalla de bienvenida", welcomePage.NavigateAsync);
        await ExtentStepAsync("Act: continuar desde la bienvenida", welcomePage.ContinueAsync);
        await ExtentStepAsync("Act: esperar la pantalla de lectura", readingPage.NavigateAsync);
        var uidBase = await ExtentStepAsync("Act: marcar la línea base de correos antes de continuar", () => OtpCorreoReader.ObtenerLineaBaseAsync());
        await ExtentStepAsync("Act: continuar desde la lectura", readingPage.ContinueAsync);
        await ExtentStepAsync("Act: esperar la pantalla de OTP", otpPage.NavigateAsync);
        var otp = await ExtentStepAsync("Act: leer el OTP del correo", () => OtpCorreoReader.ObtenerOtpAsync(uidBase, request.CorreoElectronico));
        await ExtentStepAsync("Act: ingresar el OTP", () => otpPage.EnterOtpAsync(otp));
        await ExtentStepAsync("Act: validar el OTP", otpPage.SubmitAsync);
        var completedPage = new SigningCompletedPage(Page, signingUrl!);
        await ExtentStepAsync("Assert: esperar la confirmación de firma", completedPage.NavigateAsync);

        // Assert
        if (!await completedPage.IsDisplayedAsync()) errors.Add("Signing success message not displayed.");
        Assert.AreEqual(0, errors.Count, string.Join(Environment.NewLine, errors));
    }
}
