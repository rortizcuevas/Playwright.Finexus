using System.Data;
using System.Net;
using Playwright.Finexus.Api;
using Playwright.Finexus.Base;
using Playwright.Finexus.Helpers;
using Playwright.Finexus.Interfaces;
using Playwright.Finexus.Pages;

namespace Playwright.Finexus.Tests;

/// <summary>Firma E2E de un documento PDF valido.</summary>
[TestClass]
[TestCategory("Signing")]
public class SigningPdfValidE2ETests : TestBase
{
    private static readonly string SigningDataPath = Path.Combine(AppContext.BaseDirectory, "Data/SigningData.json");

    private IProcessClient _processClient = null!;
    private IDocumentClient _documentClient = null!;

    // ── Setup ──────────────────────────────────────────────────────

    protected override async Task SetupAsync()
    {
        await base.SetupAsync();
        var token = await TokenProvider.GetTokenAsync(new AuthClient(WebApiUrl), ApiUser, ApiPassword, ApiClientCode);
        _processClient = new ProcessClient(WebApiUrl, token);
        _documentClient = new DocumentClient(WebApiUrl, token);
    }

    // ── Tests ──────────────────────────────────────────────────────

    /// <summary>
    /// TC-FIRMA-01: 1. Iniciar proceso. 2. Emitir PDF valido. 3. Bienvenida. 4. Lectura. 5. Pantalla OTP. 6. Validar OTP. 7. Mensaje de firma exitosa.
    /// </summary>
    [TestMethod]
    [Description("TC-FIRMA-01: Firma de archivo PDF valido")]
    public async Task Sign_WithValidPdf_ShouldCompleteSigning()
    {
        // Arrange
        var errors = new List<string>();
        DataRow row = DataHelper.ReadJsonData(SigningDataPath, "signPdfValid").Rows[0];
        var fileName = row["fileName"].ToString()!;
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

        var welcomePage = new WelcomePage(Page, signingUrl!);
        var readingPage = new ReadingPage(Page, signingUrl!);
        var otpPage = new OtpPage(Page, signingUrl!);

        await ExtentStepAsync("Abrir bienvenida", welcomePage.NavigateAsync);
        await ExtentStepAsync("Continuar bienvenida", welcomePage.ContinueAsync);
        await ExtentStepAsync("Esperar lectura", readingPage.NavigateAsync);
        var uidBase = await ExtentStepAsync("Marcar línea base de correo", () => OtpCorreoReader.ObtenerLineaBaseAsync());
        await ExtentStepAsync("Continuar lectura", readingPage.ContinueAsync);
        await ExtentStepAsync("Esperar pantalla OTP", otpPage.NavigateAsync);
        var otp = await ExtentStepAsync("Leer OTP de Gmail", () => OtpCorreoReader.ObtenerOtpAsync(uidBase, request.CorreoElectronico));
        await ExtentStepAsync("Ingresar OTP", () => otpPage.EnterOtpAsync(otp));
        await ExtentStepAsync("Validar OTP", otpPage.SubmitAsync);
        var completedPage = new SigningCompletedPage(Page, signingUrl!);
        await ExtentStepAsync("Esperar confirmacion de firma", completedPage.NavigateAsync);

        // Assert
        if (!await completedPage.IsDisplayedAsync()) errors.Add("Signing success message not displayed.");
        Assert.AreEqual(0, errors.Count, string.Join(Environment.NewLine, errors));
    }
}
