using System.Globalization;
using Microsoft.Playwright;

namespace Playwright.Finexus.Base;

[TestClass]
public abstract class TestBase : ApiTestBase
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    protected string WebAppUrl => GetParameter("webAppUrl");
    protected string OtpSource => GetParameter("otpSource");
    protected string RunSettingsBrowser => string.IsNullOrWhiteSpace(GetParameter("browser")) ? "chromium" : GetParameter("browser");
    protected bool Headless => bool.TryParse(GetParameter("headless"), out var value) && value;
    protected float SlowMo => float.TryParse(GetParameter("slowMo"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    protected override async Task SetupAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var browserType = RunSettingsBrowser.ToLowerInvariant() switch
        {
            "firefox" => _playwright.Firefox,
            "webkit" => _playwright.Webkit,
            _ => _playwright.Chromium
        };

        _browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions { Headless = Headless, SlowMo = SlowMo });
        Context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            IgnoreHTTPSErrors = true,
            RecordVideoDir = CaptureVideo ? Path.Combine(EvidenceDirectory, "video") : null,
            ExtraHTTPHeaders = new Dictionary<string, string> { ["Accept-Language"] = "es-CO,es;q=0.9" }
        });

        if (CaptureTrace)
        {
            await Context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });
        }

        Page = await Context.NewPageAsync();
        Page.Console += (_, msg) => _console.Add($"[{DateTime.Now:HH:mm:ss}] [{msg.Type}] {msg.Text}");
        Page.PageError += (_, err) => _console.Add($"[{DateTime.Now:HH:mm:ss}] [pageerror] {err}");
        Page.RequestFailed += (_, req) => _console.Add($"[{DateTime.Now:HH:mm:ss}] [requestfailed] {req.Method} {req.Url.Split('?')[0]} {req.Failure}");
    }

    private readonly List<string> _console = [];

    protected bool CaptureTrace => !GetParameter("captureTrace").Equals("Off", StringComparison.OrdinalIgnoreCase)
                                   && !EvidenceLevel.Equals("None", StringComparison.OrdinalIgnoreCase);
    protected bool CaptureVideo => !bool.TryParse(GetParameter("captureVideo"), out var value) || value;

    protected override async Task TearDownAsync()
    {
        if (Page is not null && CollectEvidence)
        {
            try
            {
                var path = Path.Combine(EvidenceDirectory, "failure.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
                AddEvidence(path);
                if (IsFailed) ExtentTest.Fail($"URL: {Page.Url}").AddScreenCaptureFromPath(path);
            }
            catch (Exception ex)
            {
                DiagnosticNotes.Add($"No se pudo capturar pantalla: {ex.Message}");
            }

            if (_console.Count > 0)
            {
                var consolePath = Path.Combine(EvidenceDirectory, "console.log");
                await File.WriteAllLinesAsync(consolePath, _console);
            }
        }

        if (Context is not null)
        {
            if (CaptureTrace)
            {
                try
                {
                    await Context.Tracing.StopAsync();
                }
                catch (Exception ex)
                {
                    DiagnosticNotes.Add($"No se pudo guardar el trace: {ex.Message}");
                }
            }

            var video = Page?.Video;
            await Context.CloseAsync();
            if (video is not null)
            {
                var videoPath = await video.PathAsync();
                if (CollectEvidence && File.Exists(videoPath)) AddEvidence(videoPath);
                else if (File.Exists(videoPath)) File.Delete(videoPath);
            }
        }

        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
