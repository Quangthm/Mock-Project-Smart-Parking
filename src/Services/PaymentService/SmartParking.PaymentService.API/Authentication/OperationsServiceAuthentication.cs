using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace SmartParking.PaymentService.API.Authentication;

public static class OperationsServiceAuthentication
{
    public const string Scheme = "OperationsService";
    public const string Policy = "ReservationDepositOrders";
    public const string Permission = "reservation:deposit-order";
    public const string PassFinancialTermsReadPolicy = "PassFinancialTermsRead";
    public const string PassFinancialTermsReadPermission = "pass:financial-terms:read";
    public const string PassFinancialTermsQuotePolicy = "PassFinancialTermsQuote";
    public const string PassFinancialTermsQuotePermission = "pass:financial-terms:quote";
    public const string PassClosuresCompensatePolicy = "PassClosuresCompensate";
    public const string PassClosuresCompensatePermission = "pass:closures:compensate";

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
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    await PaymentAuthChallengeWriter.WriteErrorAsync(
                        context.HttpContext,
                        StatusCodes.Status401Unauthorized,
                        "UNAUTHENTICATED",
                        "A valid Operations service credential is required.");
                },
                OnForbidden = async context =>
                {
                    await PaymentAuthChallengeWriter.WriteErrorAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "FORBIDDEN",
                        "The service is not permitted to use this capability.");
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

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policy, policy =>
                policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()
                    .RequireClaim("sub", "SmartParking.OperationService")
                    .RequireClaim("sender", "SmartParking.OperationService")
                    .RequireClaim("permission", Permission));

            options.AddPolicy(PassFinancialTermsReadPolicy, policy =>
                policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()
                    .RequireClaim("sub", "SmartParking.OperationService")
                    .RequireClaim("sender", "SmartParking.OperationService")
                    .RequireClaim("permission", PassFinancialTermsReadPermission));

            options.AddPolicy(PassFinancialTermsQuotePolicy, policy =>
                policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()
                    .RequireClaim("sub", "SmartParking.OperationService")
                    .RequireClaim("sender", "SmartParking.OperationService")
                    .RequireClaim("permission", PassFinancialTermsQuotePermission));

            options.AddPolicy(PassClosuresCompensatePolicy, policy =>
                policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()
                    .RequireClaim("sub", "SmartParking.OperationService")
                    .RequireClaim("sender", "SmartParking.OperationService")
                    .RequireClaim("permission", PassClosuresCompensatePermission));
        });
    }
}
