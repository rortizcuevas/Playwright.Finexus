using System.Net;
using Playwright.Finexus.Api;
using Playwright.Finexus.Interfaces;

namespace Playwright.Finexus.Helpers;

public static class TokenProvider
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private const string CachedTokenNote = "Token reused from cache (login performed previously)";
    private static string? _token;

    public static async Task<string> GetTokenAsync(IAuthClient client, string user, string password, string clientCode)
    {
        if (!string.IsNullOrEmpty(_token))
        {
            ApiCallRecorder.Record(new ApiCallLog { Note = CachedTokenNote });
            return _token;
        }

        await Lock.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(_token))
            {
                ApiCallRecorder.Record(new ApiCallLog { Note = CachedTokenNote });
                return _token;
            }

            var (status, body) = await client.LoginAsync(new LoginRequest
            {
                Usuario = user,
                Contrasena = password,
                Codigo = clientCode
            });

            if (status != HttpStatusCode.OK || string.IsNullOrWhiteSpace(body?.Data?.AccessToken))
            {
                throw new InvalidOperationException($"Login failed. HTTP status: {(int)status}.");
            }

            _token = body.Data.AccessToken;
            return _token;
        }
        finally
        {
            Lock.Release();
        }
    }
}
