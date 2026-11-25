namespace Playwright.Finexus.Api;

public class MailSettings
{
    public string User { get; set; } = "rtestrprueba@gmail.com";
    public string Password { get; set; } = string.Empty;
    public string Host { get; set; } = "imap.gmail.com";
    public int Port { get; set; } = 993;
    public string Subject { get; set; } = "OTP Olimpia";
    public int MaxAttempts { get; set; } = 15;
    public int DelaySeconds { get; set; } = 3;
}
