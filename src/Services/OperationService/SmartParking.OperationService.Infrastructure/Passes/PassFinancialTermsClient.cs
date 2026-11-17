using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Passes;

namespace SmartParking.OperationService.Infrastructure.Passes;

public sealed class PassFinancialTermsClient(
    HttpClient client,
    IConfiguration configuration) : IPassFinancialTermsOwner
{
    private const string Issuer = "SmartParking.OperationService";
    private const string Audience = "SmartParking.PaymentService";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PassFinancialTermsSnapshot> QuoteSnapshotAsync(
        PassFinancialTermsQuoteRequest request,
        CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/internal/pass-financial-terms/quote-snapshot")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateServiceToken());

        try
        {
            using var response = await client.SendAsync(message, ct);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorCode = ReadString(document.RootElement, "errorCode") ?? "PAYMENT_REQUEST_REJECTED";
                if ((int)response.StatusCode is 401 or 403)
                    throw new ReservationGateException((int)response.StatusCode, errorCode,
                        "PaymentService rejected the pass financial-terms request.");
                if ((int)response.StatusCode is 404 or 409 or 422)
                    throw new ReservationGateException((int)response.StatusCode, errorCode,
                        "PaymentService could not provide the requested financial-terms snapshot.");
                throw Unavailable();
            }

            if (!TryGetProperty(document.RootElement, "success", out var success) || success.ValueKind != JsonValueKind.True ||
                !TryGetProperty(document.RootElement, "data", out var data))
                throw InvalidResponse();

            return data.Deserialize<PassFinancialTermsSnapshot>(JsonOptions) ?? throw InvalidResponse();
        }
        catch (ReservationGateException)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or IOException ||
                                      error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw Unavailable();
        }
    }

    private string CreateServiceToken()
    {
        if (!configuration.GetValue<bool>("ReservationIntegration:Enabled") || client.BaseAddress is null ||
            client.BaseAddress.Scheme != Uri.UriSchemeHttps && !configuration.GetValue<bool>("ReservationIntegration:AllowHttp"))
            throw Unavailable();

        try
        {
            var pem = configuration["ReservationIntegration:OperationsPrivateKeyPem"];
            if (configuration["ReservationIntegration:OperationsPrivateKeyPath"] is { Length: > 0 } path)
                pem = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(pem)) throw Unavailable();

            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048) throw Unavailable();

            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(Issuer, Audience,
                [
                    new Claim("sub", Issuer),
                    new Claim("sender", Issuer),
                    new Claim("permission", "pass:financial-terms:quote"),
                    new Claim("role", "INTERNAL_SERVICE"),
                    new Claim("scope", "s2s"),
                    new Claim("jti", Guid.NewGuid().ToString("N"))
                ],
                now, now.AddSeconds(60), new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw Unavailable();
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static ReservationGateException InvalidResponse() =>
        new(503, "PASS_FINANCIAL_TERMS_RESPONSE_INVALID", "PaymentService returned an invalid pass financial-terms response.");

    private static ReservationGateException Unavailable() =>
        new(503, "PASS_FINANCIAL_TERMS_UNAVAILABLE", "PaymentService pass financial-terms endpoint is unavailable.");
}
