using System.Net;
using Playwright.Finexus.Interfaces;

namespace Playwright.Finexus.Api;

public class AuthClient : ApiClientBase, IAuthClient
{
    private const string LoginRoute = "api/Auth/Login";

    public AuthClient(string baseUrl) : base(baseUrl)
    {
    }

    public Task<(HttpStatusCode Status, BaseResponse<LoginResponse>? Body)> LoginAsync(LoginRequest request)
        => PostJsonAsync<LoginResponse>(LoginRoute, request);
}
