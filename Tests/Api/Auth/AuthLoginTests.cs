using Playwright.Finexus.Api;
using Playwright.Finexus.Base;
using Playwright.Finexus.Helpers;

namespace Playwright.Finexus.Tests;

[TestClass]
[TestCategory("Auth")]
public class AuthLoginTests : ApiTestBase
{
    // ── Tests ──────────────────────────────────────────────────────

    /// <summary>
    /// TC-AUTH-01: 1. Login with valid credentials. 2. Verify token is generated.
    /// </summary>
    [TestMethod]
    [Description("TC-AUTH-01: Login with valid credentials generates a token")]
    public async Task Login_WithValidCredentials_ShouldGenerateToken()
    {
        // Arrange
        var errors = new List<string>();
        var client = new AuthClient(WebApiUrl);
        string token = string.Empty;

        // Act
        try
        {
            token = await ExtentStepAsync("Login", () => TokenProvider.GetTokenAsync(client, ApiUser, ApiPassword, ApiClientCode));
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
        }

        // Assert
        if (string.IsNullOrWhiteSpace(token))
        {
            errors.Add("Token is empty.");
        }

        Assert.AreEqual(0, errors.Count, string.Join(Environment.NewLine, errors));
    }
}
