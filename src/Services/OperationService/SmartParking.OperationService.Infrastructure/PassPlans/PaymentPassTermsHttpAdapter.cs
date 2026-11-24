using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Domain.PassPlans;
using SmartParking.OperationService.Infrastructure.PassPlans.Models;

namespace SmartParking.OperationService.Infrastructure.PassPlans;

public sealed class PaymentPassTermsHttpAdapter(
    HttpClient client,
    IConfiguration configuration) : IPaymentPassTermsPort
{
    private const string Issuer = "SmartParking.OperationService";
    private const string PaymentAudience = "SmartParking.PaymentService";
    private const string ReadPermission = "pass:financial-terms:read";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<PaymentPassFinancialTermDto>> GetActiveFinancialTermsAsync(
        Guid siteId,
        Guid? planId,
        CancellationToken ct)
    {
        var token = CreateServiceToken();
        var path = planId.HasValue
            ? $"internal/pass-financial-terms?siteId={siteId:D}&planId={planId.Value:D}"
            : $"internal/pass-financial-terms?siteId={siteId:D}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw Unavailable();
            }

            var envelope = await response.Content.ReadFromJsonAsync<PaymentEnvelopeDto<IReadOnlyList<PaymentFinancialTermHttpDto>>>(
                JsonOptions,
                ct);

            if (envelope is not { Success: true, Data: not null })
            {
                throw InvalidContract();
            }

            return envelope.Data.Select(MapTerm).ToArray();
        }
        catch (PassPlanException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                   ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw Unavailable();
        }
    }

    private string CreateServiceToken()
    {
        if (!configuration.GetValue<bool>("ReservationIntegration:Enabled"))
        {
            throw Unavailable();
        }

        if (client.BaseAddress is null ||
            (client.BaseAddress.Scheme != Uri.UriSchemeHttps &&
             !configuration.GetValue<bool>("ReservationIntegration:AllowHttp")))
        {
            throw Unavailable();
        }

        try
        {
            var pem = configuration["ReservationIntegration:OperationsPrivateKeyPem"];
            if (configuration["ReservationIntegration:OperationsPrivateKeyPath"] is { Length: > 0 } privatePath)
            {
                pem = File.ReadAllText(privatePath);
            }

            if (string.IsNullOrWhiteSpace(pem))
            {
                throw Unavailable();
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048)
            {
                throw Unavailable();
            }

            var signingKey = new RsaSecurityKey(rsa.ExportParameters(true));
            var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
            {
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
            };

            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                Issuer,
                PaymentAudience,
                [
                    new Claim("sub", Issuer),
                    new Claim("sender", Issuer),
                    new Claim("permission", ReadPermission),
                    new Claim("role", "INTERNAL_SERVICE"),
                    new Claim("scope", "s2s"),
                    new Claim("jti", Guid.NewGuid().ToString("N"))
                ],
                now,
                now.AddMinutes(1),
                signingCredentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw Unavailable();
        }
    }

    private static PaymentPassFinancialTermDto MapTerm(PaymentFinancialTermHttpDto dto)
    {
        if (dto.FinancialTermId == Guid.Empty ||
            dto.PlanId == Guid.Empty ||
            dto.RefundPolicy is null ||
            dto.VehiclePrices is null)
        {
            throw InvalidContract();
        }

        return new PaymentPassFinancialTermDto(
            dto.FinancialTermId,
            dto.SiteId,
            dto.PlanId,
            dto.PlanRevision,
            dto.FinancialTermRevision,
            dto.ProductType,
            dto.Currency,
            dto.IsActive,
            dto.VehiclePrices
                .Select(vp => new PaymentPassVehiclePriceDto(vp.VehicleType, vp.FullPeriodPrice))
                .ToArray(),
            new PaymentPassRefundPolicyDto(
                dto.RefundPolicy.VoluntaryBeforeStartPercent,
                dto.RefundPolicy.VoluntaryInPeriodPercent,
                dto.RefundPolicy.FacilityClosurePercent,
                dto.RefundPolicy.RoundingMode),
            dto.EffectiveFrom);
    }

    private static PassPlanException Unavailable() =>
        new(503, "PAYMENT_SERVICE_UNAVAILABLE", "Payment financial terms service is unavailable.");

    private static PassPlanException InvalidContract() =>
        new(503, "PAYMENT_CONTRACT_INVALID", "Payment service returned an invalid financial terms payload.");
}
