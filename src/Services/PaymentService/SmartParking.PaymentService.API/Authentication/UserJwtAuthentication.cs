using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace SmartParking.PaymentService.API.Authentication;

public static class UserJwtAuthentication
{
    public const string Scheme = "UserJwt";
    public const string OwnerLotManagementPolicy = "OwnerLotManagement";
    private const string KeysUnavailableItemKey = "SmartParking.Payment.Authentication.KeysUnavailable";

    public static void AddUserJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var jwksAddress = configuration["JwtValidation:JwksUri"];
        var hasJwks = !string.IsNullOrWhiteSpace(jwksAddress);

        if (hasJwks)
        {
            var issuer = configuration["JwtValidation:Issuer"];
            var audience = configuration["JwtValidation:Audience"];
            if (string.IsNullOrWhiteSpace(issuer) ||
                string.IsNullOrWhiteSpace(audience) ||
                !Uri.TryCreate(jwksAddress, UriKind.Absolute, out var endpoint) ||
                endpoint.Scheme is not ("https" or "http"))
            {
                throw new InvalidOperationException("Configure JwtValidation issuer, audience and trusted JWKS URL.");
            }

            var allowLoopbackTestingHttp = environment.IsEnvironment("Testing") && endpoint.IsLoopback;
            if (endpoint.Scheme != Uri.UriSchemeHttps &&
                !configuration.GetValue<bool>("JwtValidation:AllowInsecureTransport") &&
                !allowLoopbackTestingHttp)
            {
                throw new InvalidOperationException("JWKS requires HTTPS unless protected/local HTTP is explicitly configured.");
            }

            services.TryAddSingleton(TimeProvider.System);
            services.AddHttpClient("SmartParking.Payment.Jwks")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            services.AddSingleton(provider => new PaymentJwksConfigurationManager(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient("SmartParking.Payment.Jwks"),
                endpoint,
                issuer,
                provider.GetRequiredService<TimeProvider>()));

            services.AddAuthentication().AddJwtBearer(Scheme);
            services.AddOptions<JwtBearerOptions>(Scheme)
                .Configure<PaymentJwksConfigurationManager>((options, manager) =>
                {
                    options.MapInboundClaims = false;
                    options.IncludeErrorDetails = false;
                    options.ConfigurationManager = manager;
                    options.RefreshOnIssuerKeyNotFound = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = audience,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        ValidateIssuerSigningKey = true,
                        RequireSignedTokens = true,
                        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = "sub",
                        RoleClaimType = "role"
                    };
                    options.Events = BuildJwtEvents();
                });
        }
        else
        {
            services.AddAuthentication().AddJwtBearer(Scheme, options =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.Events = BuildJwtEvents();
            });
        }

        services.AddAuthorization(options => options.AddPolicy(OwnerLotManagementPolicy, policy =>
            policy.AddAuthenticationSchemes(Scheme)
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                {
                    var role = context.User.FindFirst("role")?.Value
                        ?? context.User.FindFirst(ClaimTypes.Role)?.Value;
                    return string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(role, "BUSINESS_OWNER", StringComparison.OrdinalIgnoreCase);
                })));
    }

    private static JwtBearerEvents BuildJwtEvents() => new()
    {
        OnTokenValidated = context =>
        {
            if (!HasValidIdentity(context.Principal))
            {
                context.Fail("Required identity claims are invalid.");
            }

            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            for (Exception? error = context.Exception; error is not null; error = error.InnerException)
            {
                if (error is PaymentJwksUnavailableException)
                {
                    context.HttpContext.Items[KeysUnavailableItemKey] = true;
                    break;
                }
            }

            return Task.CompletedTask;
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            var unavailable = context.HttpContext.Items.ContainsKey(KeysUnavailableItemKey);
            await PaymentAuthChallengeWriter.WriteErrorAsync(
                context.HttpContext,
                unavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status401Unauthorized,
                unavailable ? "AUTHENTICATION_KEYS_UNAVAILABLE" : "UNAUTHENTICATED",
                unavailable ? "Authentication keys are temporarily unavailable." : "Authentication is required.");
        },
        OnForbidden = async context =>
        {
            await PaymentAuthChallengeWriter.WriteErrorAsync(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                "FORBIDDEN",
                "Owner parking management role is required.");
        }
    };

    private static bool HasValidIdentity(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return false;
        }

        var hasSub = HasGuidClaim(principal, "sub") || HasGuidClaim(principal, "account_id");
        var roles = principal.FindAll("role")
            .Concat(principal.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return hasSub && roles.Length >= 1;
    }

    private static bool HasGuidClaim(ClaimsPrincipal principal, string claimType)
    {
        var values = principal.FindAll(claimType).ToArray();
        return values.Length == 1 && Guid.TryParse(values[0].Value, out var id) && id != Guid.Empty;
    }
}
