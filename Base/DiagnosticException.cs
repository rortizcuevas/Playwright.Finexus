namespace Playwright.Finexus.Base;

public sealed class DiagnosticException(string message, string diagnostic, Exception inner, string? summary = null) : Exception(message, inner)
{
    public const string DiagnosticMarker = "=== DIAGNOSTICO ===";
    public const string StackMarker = "=== STACK TRACE ===";

    public string Diagnostic { get; } = diagnostic;

    public string? Summary { get; } = summary;

    public override string? StackTrace => string.IsNullOrWhiteSpace(Summary)
        ? InnerException?.StackTrace
        : $"{DiagnosticMarker}{Environment.NewLine}{Summary}{Environment.NewLine}{StackMarker}{Environment.NewLine}{InnerException?.StackTrace}";
}
