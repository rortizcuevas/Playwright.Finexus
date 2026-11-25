using System.Net;
using Playwright.Finexus.Api;

namespace Playwright.Finexus.Interfaces;

public interface IProcessClient
{
    Task<(HttpStatusCode Status, BaseResponse<IniciarProcesoResponse>? Body)> StartAsync(IniciarProcesoRequest request);
}
