namespace Playwright.Finexus.Api;

public sealed class ApiCallLog
{
    public string Method { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? RequestBody { get; init; }
    public int? StatusCode { get; init; }
    public string? ResponseBody { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public long DurationMs { get; init; }
    public string? Note { get; init; }
    public string? Step { get; set; }
}
