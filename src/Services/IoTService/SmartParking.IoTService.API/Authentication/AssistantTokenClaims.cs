using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using SmartParking.IoTService.Application.Assistant.Authorization;

namespace SmartParking.IoTService.API.Authentication;

internal static class AssistantTokenClaims
{
    // Called only after JwtBearer has verified signature, algorithm, issuer, audience and lifetime.
    // Inspect the signed JSON too: a JWT parser may collapse duplicate JSON property names.
    public static CurrentActor? Read(ClaimsPrincipal principal, string encodedPayload)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        try
        {
            using var document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(encodedPayload));
            var payload = document.RootElement;
            foreach (var name in new[] { "iss", "aud", "nbf", "exp" })
                if (payload.EnumerateObject().Count(p => p.Name == name) > 1) return null;
            string? SingleString(string name)
            {
                var properties = payload.EnumerateObject().Where(p => p.Name == name).ToArray();
                if (properties.Length != 1 || properties[0].Value.ValueKind != JsonValueKind.String) return null;
                var value = properties[0].Value.GetString();
                var claims = principal.FindAll(name).ToArray();
                return claims.Length == 1 && claims[0].Value == value ? value : null;
            }
            var user = SingleString("sub");
            var account = SingleString("account_id");
            var token = SingleString("jti");
            var role = SingleString("role");
            var expiration = payload.EnumerateObject().Where(p => p.Name == "exp").ToArray();
            if (!Guid.TryParse(user, out var userId) || userId == Guid.Empty ||
                !Guid.TryParse(account, out var accountId) || accountId == Guid.Empty ||
                !Guid.TryParse(token, out var tokenId) || tokenId == Guid.Empty ||
                string.IsNullOrWhiteSpace(role) || expiration.Length != 1 ||
                !expiration[0].Value.TryGetInt64(out var seconds)) return null;
            return new(accountId, userId, tokenId, role, DateTimeOffset.FromUnixTimeSeconds(seconds));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
