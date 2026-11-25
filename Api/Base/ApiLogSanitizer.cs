using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Playwright.Finexus.Api;

public static class ApiLogSanitizer
{
    private const string Mask = "***";

    private static readonly HashSet<string> MaskedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "contrasena", "accessToken", "refreshToken", "token"
    };

    private static readonly HashSet<string> OmittedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "contenidoBase64"
    };

    private static readonly JsonSerializerOptions OutputOptions = new() { WriteIndented = true };

    public static string Sanitize(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content ?? string.Empty;
        }

        try
        {
            var node = JsonNode.Parse(content);
            if (node is null)
            {
                return content;
            }

            Visit(node);
            return node.ToJsonString(OutputOptions);
        }
        catch (JsonException)
        {
            return content;
        }
    }

    private static void Visit(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    var value = obj[key];
                    if (MaskedProperties.Contains(key) && value is not null)
                    {
                        obj[key] = Mask;
                    }
                    else if (OmittedProperties.Contains(key) && value is not null)
                    {
                        var text = value is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : value.ToJsonString();
                        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
                        obj[key] = $"<omitted: {text.Length} chars, sha256={hash}>";
                    }
                    else if (value is not null)
                    {
                        Visit(value);
                    }
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        Visit(item);
                    }
                }
                break;
        }
    }
}
