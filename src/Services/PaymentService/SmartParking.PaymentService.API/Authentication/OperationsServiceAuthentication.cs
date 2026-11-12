using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace SmartParking.PaymentService.API.Authentication;

public static class OperationsServiceAuthentication
{
    public const string Scheme = "OperationsService";
    public const string Policy = "ReservationDepositOrders";
    public const string Permission = "reservation:deposit-order";

    public static void AddOperationsServiceAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var pem = configuration["ReservationIntegration:OperationsPublicKeyPem"];
        var enabled = configuration.GetValue<bool>("ReservationIntegration:Enabled");
        if (enabled && configuration["ReservationIntegration:OperationsPublicKeyPath"] is { Length: > 0 } publicPath)
            pem = File.ReadAllText(publicPath);

        RsaSecurityKey? key = null;
        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(pem))
                throw new InvalidOperationException("Configure the Operations service public key.");
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048)
                throw new InvalidOperationException("Operations RSA key must be at least 2048 bits.");
            key = new RsaSecurityKey(rsa.ExportParameters(false));
        }

        services.AddAuthentication().AddJwtBearer(Scheme, options =>
        {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    if (!enabled) context.Fail("Reservation integration is disabled.");
                    return Task.CompletedTask;
                }
            };
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = "SmartParking.OperationService",
                ValidateAudience = true,
                ValidAudience = "SmartParking.PaymentService",
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(5)
            };
        });

        services.AddAuthorization(options => options.AddPolicy(Policy, policy =>
            policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()
                .RequireClaim("sub", "SmartParking.OperationService")
                .RequireClaim("sender", "SmartParking.OperationService")
                .RequireClaim("permission", Permission)));
    }
}
