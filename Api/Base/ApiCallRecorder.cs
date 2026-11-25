namespace Playwright.Finexus.Api;

public static class ApiCallRecorder
{
    private static readonly AsyncLocal<Action<ApiCallLog>?> CurrentSink = new();

    public static Action<ApiCallLog>? Sink
    {
        get => CurrentSink.Value;
        set => CurrentSink.Value = value;
    }

    public static void Record(ApiCallLog log) => Sink?.Invoke(log);
}
