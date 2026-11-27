using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Infrastructure.Reservations;

internal static class FacilityServiceToken
{
    public static AuthenticationHeaderValue Create(IConfiguration configuration, string permission)
    {
        if (!configuration.GetValue<bool>("ReservationIntegration:Enabled")) throw new CryptographicException("Operations signing is disabled.");
        var pem = configuration["ReservationIntegration:OperationsPrivateKeyPem"];
        if (configuration["ReservationIntegration:OperationsPrivateKeyPath"] is { Length: > 0 } privatePath)
            pem = File.ReadAllText(privatePath);
        if (string.IsNullOrWhiteSpace(pem)) throw new CryptographicException("Operations signing key is unavailable.");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        if (rsa.KeySize < 2048) throw new CryptographicException("Operations signing key is unavailable.");
        // Each call owns and disposes its RSA key. A cached signature provider would retain it.
        var key = new RsaSecurityKey(rsa) { CryptoProviderFactory = new() { CacheSignatureProviders = false } };
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken("SmartParking.OperationService", "SmartParking.FacilityService",
            [new Claim("sub", "SmartParking.OperationService"), new Claim("sender", "SmartParking.OperationService"),
             new Claim("permission", permission), new Claim("jti", Guid.NewGuid().ToString("N"))],
            now, now.AddMinutes(1), new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }
}
