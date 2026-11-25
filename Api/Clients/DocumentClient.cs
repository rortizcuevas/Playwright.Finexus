using System.Net;
using Playwright.Finexus.Interfaces;

namespace Playwright.Finexus.Api;

public class DocumentClient : ApiClientBase, IDocumentClient
{
    private const string IssueRoute = "api/Documento/Emitir";

    public DocumentClient(string baseUrl, string bearerToken) : base(baseUrl, bearerToken)
    {
    }

    public Task<(HttpStatusCode Status, BaseResponse<EmitirDocumentoResponse>? Body)> IssueAsync(EmitirDocumentoRequest request)
        => PostJsonAsync<EmitirDocumentoResponse>(IssueRoute, request);
}
