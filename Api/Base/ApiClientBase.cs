using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Playwright.Finexus.Api;

public abstract class ApiClientBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected HttpClient Http { get; }

    protected ApiClientBase(string baseUrl, string? bearerToken = null)
    {
        Http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/")
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }

    protected async Task<(HttpStatusCode Status, BaseResponse<T>? Body)> PostJsonAsync<T>(string route, object request)
    {
        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        var url = new Uri(Http.BaseAddress!, route).ToString();
        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        string raw;
        try
        {
            response = await Http.PostAsync(route, content);
            raw = await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            ApiCallRecorder.Record(new ApiCallLog
            {
                Method = "POST",
                Url = url,
                RequestBody = ApiLogSanitizer.Sanitize(requestJson),
                ResponseBody = ex.Message,
                DurationMs = stopwatch.ElapsedMilliseconds
            });
            throw;
        }

        using var _ = response;
        stopwatch.Stop();
        ApiCallRecorder.Record(new ApiCallLog
        {
            Method = "POST",
            Url = url,
            RequestBody = ApiLogSanitizer.Sanitize(requestJson),
            StatusCode = (int)response.StatusCode,
            ResponseBody = ApiLogSanitizer.Sanitize(raw),
            DurationMs = stopwatch.ElapsedMilliseconds
        });

        BaseResponse<T>? body = null;

        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                body = JsonSerializer.Deserialize<BaseResponse<T>>(raw, JsonOptions);
            }
            catch (JsonException)
            {
            }
        }

        return (response.StatusCode, body);
    }
}
