namespace Playwright.Finexus.Helpers;

public static class UploadedFilesRecorder
{
    public static Action<string>? Sink { get; set; }

    public static void Record(string path) => Sink?.Invoke(path);
}
