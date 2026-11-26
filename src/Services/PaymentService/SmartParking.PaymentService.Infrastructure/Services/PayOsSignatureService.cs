using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace SmartParking.PaymentService.Infrastructure.Services;

public static class PayOsSignatureService
{
    public static string ComputeHmacSha256(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static bool VerifySignature(string calculatedSignature, string providedSignature)
    {
        if (string.IsNullOrEmpty(calculatedSignature) || string.IsNullOrEmpty(providedSignature))
        {
            return false;
        }

        var calcBytes = Encoding.UTF8.GetBytes(calculatedSignature);
        var provBytes = Encoding.UTF8.GetBytes(providedSignature);

        if (calcBytes.Length != provBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(calcBytes, provBytes);
    }

    public static string CreateSignatureFromDictionary(IDictionary<string, object?> data, string checksumKey)
    {
        // 1. Sort dictionary keys alphabetically
        var sorted = data
            .Where(kvp => kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.ToString()))
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => $"{kvp.Key}={kvp.Value}");

        // 2. Format: key1=value1&key2=value2
        var dataString = string.Join("&", sorted);

        // 3. Compute HMAC-SHA256
        return ComputeHmacSha256(dataString, checksumKey);
    }

    public static string CreateCheckoutSignature(long amount, string cancelUrl, string description, long orderCode, string returnUrl, string checksumKey)
    {
        var dict = new Dictionary<string, object?>
        {
            ["amount"] = amount,
            ["cancelUrl"] = cancelUrl,
            ["description"] = description,
            ["orderCode"] = orderCode,
            ["returnUrl"] = returnUrl
        };

        return CreateSignatureFromDictionary(dict, checksumKey);
    }

    public static string? CreatePaymentWebhookSignature(JsonElement data, string checksumKey)
        => CreatePaymentRequestDataSignature(data, checksumKey, includeTransactions: false);

    public static string? CreatePaymentLinkResponseSignature(JsonElement data, string checksumKey)
        => CreatePaymentRequestDataSignature(data, checksumKey, includeTransactions: true);

    private static string? CreatePaymentRequestDataSignature(JsonElement data, string checksumKey, bool includeTransactions)
    {
        if (data.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(checksumKey))
            return null;

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in data.EnumerateObject())
        {
            // Payment-request callbacks use integers for order codes and VND values.
            // Reject shapes whose signature representation is outside this contract.
            var value = field.Value.ValueKind switch
            {
                JsonValueKind.Null => "",
                JsonValueKind.String => field.Value.GetString() is "null" or "undefined" ? "" : field.Value.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number when field.Value.TryGetInt64(out var number) => number.ToString(CultureInfo.InvariantCulture),
                JsonValueKind.Array when includeTransactions && field.Name == "transactions" => FormatTransactions(field.Value),
                _ => null
            };
            if (value is null || !fields.TryAdd(field.Name, value)) return null;
        }

        return ComputeHmacSha256(string.Join("&", fields.Select(field => $"{field.Key}={field.Value}")), checksumKey);
    }

    private static readonly JsonSerializerOptions TransactionSigningJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string? FormatTransactions(JsonElement data)
    {
        var items = new List<SortedDictionary<string, JsonElement>>();
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                return null;
            var fields = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var field in item.EnumerateObject())
            {
                if (field.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array ||
                    !fields.TryAdd(field.Name, field.Value))
                    return null;
            }
            items.Add(fields);
        }
        return JsonSerializer.Serialize(items, TransactionSigningJson);
    }
}
