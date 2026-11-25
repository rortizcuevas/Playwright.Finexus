namespace Playwright.Finexus.Helpers;

public static class FileHelper
{
    private const string TestFilesFolder = "TestFiles";

    public static string GetTestFilePath(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, TestFilesFolder, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Test file not found: {path}");
        }

        UploadedFilesRecorder.Record(path);
        return path;
    }

    public static async Task<string> ToBase64Async(string path)
    {
        UploadedFilesRecorder.Record(path);
        return Convert.ToBase64String(await File.ReadAllBytesAsync(path));
    }
}
