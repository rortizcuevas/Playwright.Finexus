using System.Net;
using Playwright.Finexus.Interfaces;

namespace Playwright.Finexus.Api;

public class ProcessClient : ApiClientBase, IProcessClient
{
    private const string StartRoute = "api/Procesos/Iniciar";

    public ProcessClient(string baseUrl, string bearerToken) : base(baseUrl, bearerToken)
    {
    }

    public Task<(HttpStatusCode Status, BaseResponse<IniciarProcesoResponse>? Body)> StartAsync(IniciarProcesoRequest request)
        => PostJsonAsync<IniciarProcesoResponse>(StartRoute, request);
}
