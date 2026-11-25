using AventStack.ExtentReports;
using AventStack.ExtentReports.Reporter;

namespace Playwright.Finexus.Helpers;

[TestClass]
public class ExtentReportManager
{
    private static readonly object SyncRoot = new();
    private static ExtentReports? _extent;

    public static ExtentReports Instance
    {
        get
        {
            lock (SyncRoot)
            {
                if (_extent is null)
                {
                    var reportDir = Path.Combine(AppContext.BaseDirectory, "Reports");
                    Directory.CreateDirectory(reportDir);
                    _extent = new ExtentReports();
                    _extent.AttachReporter(new ExtentSparkReporter(Path.Combine(reportDir, "extent-report.html")));
                }

                return _extent;
            }
        }
    }

    [AssemblyCleanup]
    public static void Flush()
    {
        lock (SyncRoot)
        {
            _extent?.Flush();
        }
    }
}
