using System.Data;
using System.Net;
using Playwright.Finexus.Api;
using Playwright.Finexus.Base;
using Playwright.Finexus.Helpers;
using Playwright.Finexus.Interfaces;

namespace Playwright.Finexus.Tests;

/// <summary>Emision de documentos invalidos: ningun caso debe ser aceptado.</summary>
[TestClass]
[TestCategory("Document")]
public class DocumentIssueInvalidFileTests : ApiTestBase
{
    private static readonly string DocumentDataPath = Path.Combine(AppContext.BaseDirectory, "Data/DocumentData.json");

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

    [TestMethod]
    [Description("TC-DOCUMENT-01: Carga de PDF protegido con contrasena")]
    public Task Issue_WithPasswordProtectedPdf_ShouldBeRejected() => RunRejectedIssueAsync("issuePasswordProtectedPdf");

    [TestMethod]
    [Description("TC-DOCUMENT-02: Carga de PDF corrupto")]
    public Task Issue_WithCorruptPdf_ShouldBeRejected() => RunRejectedIssueAsync("issueCorruptPdf");

    [TestMethod]
    [Description("TC-DOCUMENT-03: Carga sin ingresar archivo")]
    public Task Issue_WithoutFile_ShouldBeRejected() => RunRejectedIssueAsync("issueNoFile");

    [TestMethod]
    [Description("TC-DOCUMENT-04: Carga de archivo Word")]
    public Task Issue_WithWordFile_ShouldBeRejected() => RunRejectedIssueAsync("issueWord");

    [TestMethod]
    [Description("TC-DOCUMENT-05: Carga de archivo Excel")]
    public Task Issue_WithExcelFile_ShouldBeRejected() => RunRejectedIssueAsync("issueExcel");

    // ── Helpers ────────────────────────────────────────────────────

    private async Task RunRejectedIssueAsync(string dataKey)
    {
        // Arrange
        var errors = new List<string>();
        DataRow row = DataHelper.ReadJsonData(DocumentDataPath, dataKey).Rows[0];
        var fileName = row["fileName"].ToString()!;
        var startRequest = new IniciarProcesoRequest
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
        var start = await ExtentStepAsync("Iniciar proceso", () => _processClient.StartAsync(startRequest));
        if (start.Status != HttpStatusCode.Created) errors.Add($"Iniciar: expected 201, got {(int)start.Status}.");
        var processSerial = start.Body?.Data?.ProcesoSerial;
        if (string.IsNullOrWhiteSpace(processSerial))
        {
            Assert.Fail(string.Join(Environment.NewLine, errors.Append("procesoSerial is empty.")));
        }

        var issueRequest = new EmitirDocumentoRequest { ProcesoSerial = processSerial! };
        if (!string.IsNullOrEmpty(fileName))
        {
            var content = await FileHelper.ToBase64Async(FileHelper.GetTestFilePath(fileName));
            if (row.Table.Columns.Contains("truncateBase64Percent"))
            {
                var keepPercent = Convert.ToInt32(row["truncateBase64Percent"]);
                var keepLength = content.Length * keepPercent / 100 / 4 * 4;
                content = content[..keepLength];
            }
            issueRequest.Documentos.Add(new DocumentoItem { NombreArchivo = Path.GetFileName(fileName), ContenidoBase64 = content });
        }

        var issue = await ExtentStepAsync("Emitir documento invalido", () => _documentClient.IssueAsync(issueRequest));
        TestContext.WriteLine($"Emitir status: {(int)issue.Status}");

        // Assert
        if (issue.Status == HttpStatusCode.OK) errors.Add("Emitir: document was accepted (200).");
        if (issue.Body?.Success == true) errors.Add("Emitir: Success is true.");
        if (!string.IsNullOrWhiteSpace(issue.Body?.Data?.Url)) errors.Add("Emitir: signing url was returned.");
        Assert.AreEqual(0, errors.Count, string.Join(Environment.NewLine, errors));
    }
}
