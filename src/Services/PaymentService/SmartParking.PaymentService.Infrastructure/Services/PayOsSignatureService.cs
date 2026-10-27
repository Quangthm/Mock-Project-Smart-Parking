using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
}
