using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.API.Middleware;

namespace SmartParking.OperationService.API.Authentication;

public static class PassPlanServiceAuthentication
{
    public const string Scheme = "OperationsService";
    public const string Policy = "PassPlanFreeze";
    public const string Permission = "pass:plan:freeze";

    public static void AddPassPlanServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("ReservationIntegration:Enabled");
        RsaSecurityKey? key = null;

        if (enabled)
        {
            var pem = ResolvePublicOrPrivatePem(configuration);
            if (string.IsNullOrWhiteSpace(pem))
            {
                throw new InvalidOperationException("Configure the Operations service public key.");
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048)
            {
                throw new InvalidOperationException("Operations RSA key must be at least 2048 bits.");
            }

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
                    if (!enabled)
                    {
                        context.Fail("Pass plan internal service authentication is disabled.");
                    }
                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    await ApiErrors.WriteAsync(
                        context.HttpContext,
                        StatusCodes.Status401Unauthorized,
                        "UNAUTHENTICATED",
                        "A valid Operations service credential is required.",
                        ct: context.HttpContext.RequestAborted);
                },
                OnForbidden = async context =>
                {
                    await ApiErrors.WriteAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "FORBIDDEN",
                        "The service is not permitted to use this capability.",
                        ct: context.HttpContext.RequestAborted);
                }
            };
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = "SmartParking.OperationService",
                ValidateAudience = true,
                ValidAudience = "SmartParking.OperationService",
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(5)
            };
        });

        services.AddAuthorization(options => options.AddPolicy(Policy, policy =>
            policy.AddAuthenticationSchemes(Scheme)
                .RequireAuthenticatedUser()
                .RequireClaim("sub", "SmartParking.OperationService")
                .RequireClaim("sender", "SmartParking.OperationService")
                .RequireClaim("permission", Permission)));
    }

    private static string? ResolvePublicOrPrivatePem(IConfiguration configuration)
    {
        var pem = configuration["ReservationIntegration:OperationsPublicKeyPem"];
        if (configuration["ReservationIntegration:OperationsPublicKeyPath"] is { Length: > 0 } publicPath)
        {
            pem = File.ReadAllText(publicPath);
        }

        if (!string.IsNullOrWhiteSpace(pem))
        {
            return pem;
        }

        pem = configuration["ReservationIntegration:OperationsPrivateKeyPem"];
        if (configuration["ReservationIntegration:OperationsPrivateKeyPath"] is { Length: > 0 } privatePath)
        {
            pem = File.ReadAllText(privatePath);
        }

        return pem;
    }
}
