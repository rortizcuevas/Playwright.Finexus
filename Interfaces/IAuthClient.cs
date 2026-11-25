using System.Net;
using Playwright.Finexus.Api;

namespace Playwright.Finexus.Interfaces;

public interface IAuthClient
{
    Task<(HttpStatusCode Status, BaseResponse<LoginResponse>? Body)> LoginAsync(LoginRequest request);
}
