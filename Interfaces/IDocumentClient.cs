using System.Net;
using Playwright.Finexus.Api;

namespace Playwright.Finexus.Interfaces;

public interface IDocumentClient
{
    Task<(HttpStatusCode Status, BaseResponse<EmitirDocumentoResponse>? Body)> IssueAsync(EmitirDocumentoRequest request);
}
