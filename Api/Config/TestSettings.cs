using Microsoft.Extensions.Configuration;

namespace Playwright.Finexus.Api;

public static class TestSettings
{
    private static readonly Lazy<IConfigurationRoot> Configuration = new(() => new ConfigurationBuilder()
        .AddUserSecrets(typeof(TestSettings).Assembly, optional: true)
        .AddEnvironmentVariables()
        .Build());

    public static MailSettings Mail => Configuration.Value.GetSection("Mail").Get<MailSettings>() ?? new MailSettings();
}
