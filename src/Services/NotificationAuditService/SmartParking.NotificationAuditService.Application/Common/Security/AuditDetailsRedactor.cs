using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartParking.NotificationAuditService.Application.Common.Security;

public static class AuditDetailsRedactor
{
    private const string RedactedValue = "[REDACTED]";

    private static readonly string[] SensitiveKeyTokens =
    {
        "password",
        "passcode",
        "pwd",
        "secret",
        "token",
        "otp",
        "privatekey",
        "apikey",
        "signature",
        "authorization",
        "cookie",
        "recoverycode",
        "backupcode",
        "totp",
        "credential",
        "pin"
    };

    public static string Redact(string? rawDetails)
    {
        if (string.IsNullOrWhiteSpace(rawDetails))
        {
            return "{}";
        }

        try
        {
            var node = JsonNode.Parse(rawDetails);
            if (node is null)
            {
                return "{}";
            }

            RedactNode(node);
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
        catch (JsonException)
        {
            return "{\"raw\":\"[UNPARSEABLE_REDACTED]\"}";
        }
    }

    private static void RedactNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (IsSensitiveKey(property.Key))
                    {
                        obj[property.Key] = RedactedValue;
                    }
                    else
                    {
                        RedactNode(property.Value);
                    }
                }
                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    RedactNode(item);
                }
                break;
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        var normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
                            .Replace("-", string.Empty, StringComparison.Ordinal)
                            .ToLowerInvariant();

        return SensitiveKeyTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal));
    }
}
