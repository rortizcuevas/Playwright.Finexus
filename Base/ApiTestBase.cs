using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AventStack.ExtentReports;
using Playwright.Finexus.Api;
using Playwright.Finexus.Helpers;
using Playwright.Finexus.Interfaces;

namespace Playwright.Finexus.Base;

[TestClass]
public abstract partial class ApiTestBase
{
    public TestContext TestContext { get; set; } = null!;

    protected ExtentTest ExtentTest { get; private set; } = null!;
    protected IDataHelper DataHelper { get; } = new DataHelpers();

    protected string WebApiUrl => GetParameter("webApiUrl");
    protected string ApiUser => GetParameter("apiUser");
    protected string ApiPassword => Environment.GetEnvironmentVariable("FINEXUS_API_PASSWORD") ?? GetParameter("apiPassword");
    protected string ApiClientCode => GetParameter("apiClientCode");

    protected string EvidenceLevel => string.IsNullOrWhiteSpace(GetParameter("evidenceLevel")) ? "OnFailure" : GetParameter("evidenceLevel");
    protected double MaxEvidenceMbPerTest => double.TryParse(GetParameter("maxEvidenceMbPerTest"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 5;
    protected bool IsFailed => TestContext.CurrentTestOutcome != UnitTestOutcome.Passed;
    protected bool CollectEvidence => !EvidenceLevel.Equals("None", StringComparison.OrdinalIgnoreCase) && (IsFailed || EvidenceLevel.Equals("Full", StringComparison.OrdinalIgnoreCase));

    protected List<ApiCallLog> ApiCalls { get; private set; } = [];
    protected string EvidenceDirectory { get; private set; } = string.Empty;
    protected string? CurrentStep { get; private set; }
    protected List<string> DiagnosticNotes { get; } = [];

    private readonly List<(string Step, bool Passed, string? Error)> _steps = [];
    private string? _failedStepError;
    private readonly List<string> _uploadedFiles = [];

    [TestInitialize]
    public Task InitializeAsync()
    {
        var method = GetType().GetMethod(TestContext.TestName ?? string.Empty);
        var description = method?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
        ExtentTest = ExtentReportManager.Instance.CreateTest(TestContext.TestDisplayName ?? TestContext.TestName, description);
        foreach (var category in method?.GetCustomAttributes<TestCategoryAttribute>().SelectMany(c => c.TestCategories) ?? [])
        {
            ExtentTest.AssignCategory(category);
        }

        EvidenceDirectory = Path.Combine(AppContext.BaseDirectory, "Evidence", Slug(TestContext.TestName), ResolveCaseFolder());
        if (Directory.Exists(EvidenceDirectory))
        {
            Directory.Delete(EvidenceDirectory, true);
        }
        Directory.CreateDirectory(EvidenceDirectory);

        ApiCalls = [];
        ApiCallRecorder.Sink = LogApiCall;
        _uploadedFiles.Clear();
        UploadedFilesRecorder.Sink = RecordUploadedFile;

        return SetupAsync();
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await TearDownAsync();
        if (IsFailed)
        {
            ExtentTest.Fail($"Outcome: {TestContext.CurrentTestOutcome}");
        }

        WriteEvidenceFiles();
        ApiCallRecorder.Sink = null;
        UploadedFilesRecorder.Sink = null;
    }

    protected void RecordUploadedFile(string path)
    {
        lock (_uploadedFiles)
        {
            if (!_uploadedFiles.Contains(path)) _uploadedFiles.Add(path);
        }
    }

    private static string? _projectRoot;

    protected static string ProjectRoot
    {
        get
        {
            if (_projectRoot is not null) return _projectRoot;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && dir.GetFiles("*.csproj").Length == 0) dir = dir.Parent;
            return _projectRoot = dir?.FullName ?? AppContext.BaseDirectory;
        }
    }

    protected static string RelativeToProject(string path)
        => Path.GetRelativePath(ProjectRoot, path).Replace('\\', '/');

    private void AppendFileSections(StringBuilder sb, bool final)
    {
        var rel = RelativeToProject(EvidenceDirectory);
        sb.AppendLine("## Ubicación de evidencias");
        sb.AppendLine();
        sb.AppendLine($"- Captura de pantalla: `{rel}/failure.png`");
        sb.AppendLine($"- Video: `{rel}/*.webm`");
        sb.AppendLine($"- Seguimiento (api-calls.json, api-calls.html, console.log): `{rel}/seguimiento.zip`");
        sb.AppendLine($"- Diagnóstico: `{rel}/diagnostico.md`");
        sb.AppendLine($"- Resumen: `{rel}/resumen.md`");
        sb.AppendLine();

        List<string> uploaded;
        lock (_uploadedFiles) uploaded = [.. _uploadedFiles];
        sb.AppendLine("## Archivos cargados");
        sb.AppendLine();
        if (uploaded.Count == 0) sb.AppendLine("- Ninguno");
        foreach (var file in uploaded) sb.AppendLine($"- `{RelativeToProject(file)}`");
        sb.AppendLine();

        if (final && Directory.Exists(EvidenceDirectory))
        {
            sb.AppendLine("## Archivos generados");
            sb.AppendLine();
            foreach (var file in Directory.GetFiles(EvidenceDirectory, "*", SearchOption.AllDirectories))
            {
                sb.AppendLine($"- `{RelativeToProject(file)}`");
            }
            sb.AppendLine();
        }
    }

    private string ResolveCaseFolder()
    {
        var displayName = TestContext.TestDisplayName;
        return string.IsNullOrWhiteSpace(displayName) || displayName == TestContext.TestName ? "default" : Slug(displayName);
    }

    protected static string Slug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "default";
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        {
            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        }

        var slug = SlugDashes().Replace(builder.ToString(), "-").Trim('-');
        return slug.Length > 60 ? slug[..60].TrimEnd('-') : slug;
    }

    private void LogApiCall(ApiCallLog log)
    {
        log.Step = CurrentStep;
        lock (ApiCalls)
        {
            ApiCalls.Add(log);
        }

        if (log.Note is not null)
        {
            TestContext.WriteLine(log.Note);
            ExtentTest.Info(log.Note);
            return;
        }

        var header = $"{log.Method} {log.Url} -> {log.StatusCode?.ToString() ?? "N/A"} ({log.DurationMs} ms)";
        TestContext.WriteLine(header);
        ExtentTest.Info(header);
    }

    protected void AddEvidence(string path)
    {
        if (File.Exists(path))
        {
            TestContext.AddResultFile(path);
        }
    }

    protected long EvidenceSizeBytes()
        => Directory.Exists(EvidenceDirectory) ? Directory.GetFiles(EvidenceDirectory).Sum(f => new FileInfo(f).Length) : 0;

    protected bool EvidenceBudgetExceeded(long extraBytes = 0)
        => EvidenceSizeBytes() + extraBytes > MaxEvidenceMbPerTest * 1024 * 1024;

    private void WriteEvidenceFiles()
    {
        List<ApiCallLog> calls;
        lock (ApiCalls)
        {
            calls = [.. ApiCalls];
        }

        var resumen = Path.Combine(EvidenceDirectory, "resumen.md");
        File.WriteAllText(resumen, BuildSummary(calls));

        if (EvidenceLevel.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (CollectEvidence)
        {
            var trackingFiles = new List<string> { resumen };
            if (IsFailed)
            {
                var diagnostico = Path.Combine(EvidenceDirectory, "diagnostico.md");
                var diagnosticText = BuildDiagnostic(calls, true);
                File.WriteAllText(diagnostico, diagnosticText);
                trackingFiles.Add(diagnostico);
                TestContext.WriteLine(diagnosticText);
            }

            if (calls.Count > 0)
            {
                var json = Path.Combine(EvidenceDirectory, "api-calls.json");
                File.WriteAllText(json, JsonSerializer.Serialize(calls, new JsonSerializerOptions { WriteIndented = true }));
                trackingFiles.Add(json);

                var html = Path.Combine(EvidenceDirectory, "api-calls.html");
                File.WriteAllText(html, BuildApiCallsHtml(calls));
                trackingFiles.Add(html);
            }

            var consolePath = Path.Combine(EvidenceDirectory, "console.log");
            if (File.Exists(consolePath)) trackingFiles.Add(consolePath);

            if (trackingFiles.Count > 0)
            {
                var zipPath = Path.Combine(EvidenceDirectory, "seguimiento.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var file in trackingFiles)
                    {
                        zip.CreateEntryFromFile(file, Path.GetFileName(file));
                    }
                }
                AddEvidence(zipPath);
            }
        }
    }

    private string BuildSummary(List<ApiCallLog> calls)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {TestContext.TestDisplayName ?? TestContext.TestName}");
        sb.AppendLine();
        sb.AppendLine($"- Resultado: {TestContext.CurrentTestOutcome}");
        sb.AppendLine($"- Pasos: {_steps.Count} ({_steps.Count(s => s.Passed)} correctos)");
        sb.AppendLine($"- Llamadas API: {calls.Count(c => c.Note is null)}");
        return sb.ToString();
    }

    private string BuildDiagnostic(List<ApiCallLog> calls, bool final = false)
    {
        var apiCalls = calls.Where(c => c.Note is null).ToList();
        var failedCall = apiCalls.LastOrDefault(c => c.StatusCode is null or >= 400);
        var category = Classify(failedCall);
        var failedStep = _steps.LastOrDefault(s => !s.Passed);
        var fingerprintSource = $"{failedCall?.Method} {StripQuery(failedCall?.Url)} {failedCall?.StatusCode} {NormalizeMessage(_failedStepError)}";
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource)))[..12].ToLowerInvariant();

        var sb = new StringBuilder();
        sb.AppendLine($"# Diagnóstico: {TestContext.TestDisplayName ?? TestContext.TestName}");
        sb.AppendLine();
        sb.AppendLine($"- Resultado: {TestContext.CurrentTestOutcome}");
        sb.AppendLine($"- Categoría: {category}");
        sb.AppendLine($"- Huella: {fingerprint}");
        sb.AppendLine($"- Paso fallido: {failedStep.Step ?? "N/A"}");
        sb.AppendLine($"- Error: {_failedStepError ?? "N/A"}");
        sb.AppendLine($"- Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (_steps.Count > 0)
        {
            sb.AppendLine("## Pasos");
            sb.AppendLine();
            sb.AppendLine("| # | Paso | Estado |");
            sb.AppendLine("|---|---|---|");
            for (var i = 0; i < _steps.Count; i++)
            {
                sb.AppendLine($"| {i + 1} | {_steps[i].Step} | {(_steps[i].Passed ? "OK" : "FALLO")} |");
            }
            sb.AppendLine();
        }

        if (apiCalls.Count > 0)
        {
            sb.AppendLine("## Llamadas API");
            sb.AppendLine();
            sb.AppendLine("| # | Paso | Método | URL | Estado | ms |");
            sb.AppendLine("|---|---|---|---|---|---|");
            for (var i = 0; i < apiCalls.Count; i++)
            {
                var c = apiCalls[i];
                sb.AppendLine($"| {i + 1} | {c.Step ?? "-"} | {c.Method} | {StripQuery(c.Url)} | {c.StatusCode?.ToString() ?? "N/A"} | {c.DurationMs} |");
            }
            sb.AppendLine();

            if (failedCall is not null)
            {
                sb.AppendLine("## Última llamada con error");
                sb.AppendLine();
                sb.AppendLine("```json");
                sb.AppendLine(Truncate(failedCall.ResponseBody, 4000));
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        if (DiagnosticNotes.Count > 0)
        {
            sb.AppendLine("## Notas");
            sb.AppendLine();
            foreach (var note in DiagnosticNotes)
            {
                sb.AppendLine($"- {note}");
            }
            sb.AppendLine();
        }

        AppendFileSections(sb, final);
        return sb.ToString();
    }

    private string BuildDiagnosticSummary(List<ApiCallLog> calls)
    {
        var failedCall = calls.Where(c => c.Note is null).LastOrDefault(c => c.StatusCode is null or >= 400);
        var failedStep = _steps.LastOrDefault(s => !s.Passed);
        var lines = new List<string>
        {
            $"Categoría: {Classify(failedCall)}",
            $"Paso fallido: {failedStep.Step ?? "N/A"}"
        };
        if (failedCall is not null)
        {
            lines.Add($"Llamada: {failedCall.Method} {StripQuery(failedCall.Url)} -> {failedCall.StatusCode?.ToString() ?? "sin respuesta"} ({failedCall.DurationMs} ms)");
            var body = Truncate(failedCall.ResponseBody, 300)?.ReplaceLineEndings(" ").Trim();
            if (!string.IsNullOrEmpty(body)) lines.Add($"Respuesta: {body}");
        }
        lines.AddRange(DiagnosticNotes.Take(5).Select(n => $"Nota: {n.ReplaceLineEndings(" ")}"));
        lines.Add("Detalle completo: adjunto diagnostico.md");
        return string.Join(Environment.NewLine, lines);
    }

    protected virtual string Classify(ApiCallLog? failedCall)
    {
        var error = _failedStepError ?? string.Empty;
        if (failedCall is not null && failedCall.StatusCode is null)
        {
            return "Entorno";
        }
        if (failedCall?.StatusCode is >= 500 || (failedCall is not null && error.Contains("expected", StringComparison.OrdinalIgnoreCase) && failedCall.StatusCode is >= 400))
        {
            return "API";
        }
        if (error.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "UI-Timeout";
        }
        if (error.Contains("net::", StringComparison.OrdinalIgnoreCase) || error.Contains("ERR_", StringComparison.Ordinal))
        {
            return "Entorno";
        }
        if (error.Contains("data", StringComparison.OrdinalIgnoreCase) || error.Contains("datos", StringComparison.OrdinalIgnoreCase))
        {
            return "Datos";
        }
        return failedCall is not null ? "API" : "UI-Assert";
    }

    private static string BuildApiCallsHtml(List<ApiCallLog> calls)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>API calls</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial;font-size:13px}details{margin:4px 0;border:1px solid #ccc;padding:4px}.err summary{color:#b00}pre{background:#f6f6f6;padding:6px;white-space:pre-wrap}</style></head><body>");
        foreach (var c in calls)
        {
            if (c.Note is not null)
            {
                sb.AppendLine($"<p><i>{WebUtility.HtmlEncode(c.Note)}</i></p>");
                continue;
            }

            var cls = c.StatusCode is null or >= 400 ? " class=\"err\"" : string.Empty;
            sb.AppendLine($"<details{cls}><summary>[{WebUtility.HtmlEncode(c.Step ?? "-")}] {c.Method} {WebUtility.HtmlEncode(c.Url)} → {c.StatusCode?.ToString() ?? "N/A"} ({c.DurationMs} ms)</summary>");
            sb.AppendLine($"<b>Request</b><pre>{WebUtility.HtmlEncode(c.RequestBody ?? string.Empty)}</pre>");
            sb.AppendLine($"<b>Response</b><pre>{WebUtility.HtmlEncode(c.ResponseBody ?? string.Empty)}</pre></details>");
        }
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string StripQuery(string? url) => url is null ? string.Empty : url.Split('?')[0];

    private static string NormalizeMessage(string? message)
        => message is null ? string.Empty : Digits().Replace(message, "#");

    private static string Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max] + "...";

    protected virtual Task SetupAsync() => Task.CompletedTask;

    protected virtual Task TearDownAsync() => Task.CompletedTask;

    protected virtual Task OnStepCompletedAsync(string stepName, bool passed) => Task.CompletedTask;

    protected async Task<T> ExtentStepAsync<T>(string stepName, Func<Task<T>> action)
    {
        CurrentStep = stepName;
        try
        {
            var result = await action();
            ExtentTest.Pass(stepName);
            _steps.Add((stepName, true, null));
            await OnStepCompletedAsync(stepName, true);
            return result;
        }
        catch (Exception ex)
        {
            ExtentTest.Fail($"{stepName}: {ex.Message}");
            _steps.Add((stepName, false, ex.Message));
            _failedStepError ??= $"{stepName}: {ex.Message.Split('\n')[0].Trim()}";
            await OnStepCompletedAsync(stepName, false);
            if (ex is DiagnosticException) throw;

            List<ApiCallLog> calls;
            lock (ApiCalls) calls = [.. ApiCalls];
            var diagnostic = BuildDiagnostic(calls);
            TestContext.WriteLine(diagnostic);
            throw new DiagnosticException($"{stepName}: {ex.Message}", diagnostic, ex, BuildDiagnosticSummary(calls));
        }
        finally
        {
            CurrentStep = null;
        }
    }

    protected Task ExtentStepAsync(string stepName, Func<Task> action)
        => ExtentStepAsync(stepName, async () => { await action(); return true; });

    protected string GetParameter(string name)
        => TestContext.Properties[name]?.ToString() ?? string.Empty;

    [GeneratedRegex("-{2,}")]
    private static partial Regex SlugDashes();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}
