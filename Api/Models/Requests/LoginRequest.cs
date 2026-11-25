namespace Playwright.Finexus.Api;

public class LoginRequest
{
    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
}
