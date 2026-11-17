using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace SmartParking.PaymentService.API.Authentication;

/// <summary>
/// Registers the "UserServiceJwt" JwtBearer scheme used to authorize admin payment
/// integration endpoints against UserService-issued RS256 access tokens. Public key
/// material is obtained from a trusted JWKS endpoint or a configured public PEM.
/// This scheme does not change the service's default authentication scheme.
/// </summary>
public static class UserServiceJwtAuthentication
{
    public const string SchemeName = "UserServiceJwt";

    private const string JwksHttpClientName = "PaymentService.Jwks";
    private const string DefaultIssuer = "SmartParking.UserService";
    private const string DefaultAudience = "SmartParking";
    // Shared with the OperationService challenge contract: key-dependency outage flag.
    private const string KeysUnavailableItemKey = "SmartParking.Authentication.KeysUnavailable";
    private const string KeysUnavailableCode = "AUTHENTICATION_KEYS_UNAVAILABLE";

    public static IServiceCollection AddUserServiceJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("JwtValidation");
        var issuer = section["Issuer"] is { Length: > 0 } configuredIssuer
            ? configuredIssuer
            : DefaultIssuer;
        var audience = section["Audience"] is { Length: > 0 } configuredAudience
            ? configuredAudience
            : DefaultAudience;
        var jwksUri = section["JwksUri"];
        var publicKeyPem = section["PublicKeyPem"];

        // Only the officially configured issuer/audience plus the canonical defaults are
        // trusted; no development/demo audience is accepted.
        var validIssuers = new[] { issuer, DefaultIssuer }.Distinct(StringComparer.Ordinal).ToArray();
        var validAudiences = new[] { audience, DefaultAudience }.Distinct(StringComparer.Ordinal).ToArray();

        Uri? jwksEndpoint = null;
        if (Uri.TryCreate(jwksUri, UriKind.Absolute, out var candidate) &&
            candidate.Scheme is "http" or "https")
        {
            if (candidate.Scheme != "https" && !IsInsecureTransportAllowed(configuration, candidate))
                throw new InvalidOperationException(
                    "JwtValidation:JwksUri requires HTTPS unless protected/local HTTP is explicitly " +
                    "configured with JwtValidation:AllowInsecureTransport=true.");
            jwksEndpoint = candidate;
        }

        var rsaKey = jwksEndpoint is null ? TryLoadPublicKey(publicKeyPem) : null;
        var hasTrustMaterial = jwksEndpoint is not null || rsaKey is not null;

        services.TryAddSingleton(TimeProvider.System);

        services.AddAuthentication()
            .AddJwtBearer(SchemeName, options =>
            {
                options.RequireHttpsMetadata = false;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.RefreshOnIssuerKeyNotFound = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = validIssuers,
                    ValidateAudience = true,
                    ValidAudiences = validAudiences,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    IssuerSigningKeys = rsaKey is null ? [] : [rsaKey]
                };
                options.Events = new JwtBearerEvents
                {
                    // Core actor claims are mandatory: a non-blank user id (sub) and a
                    // non-blank role. Anything else is rejected as an invalid token.
                    OnTokenValidated = context =>
                    {
                        if (!HasIdentity(context.Principal))
                            context.Fail("Required identity claims are missing or invalid.");
                        return Task.CompletedTask;
                    },
                    // A JWKS/key-dependency outage is recorded so the challenge can be
                    // distinguished from a genuinely invalid token.
                    OnAuthenticationFailed = context =>
                    {
                        for (Exception? error = context.Exception; error is not null; error = error.InnerException)
                        {
                            if (error is not JwksUnavailableException) continue;
                            context.HttpContext.Items[KeysUnavailableItemKey] = true;
                            // Convert the key-dependency outage into a normal authentication
                            // failure so the pipeline proceeds to the challenge (503) instead of
                            // surfacing the raw exception.
                            context.Fail(error);
                            break;
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        if (!context.HttpContext.Items.ContainsKey(KeysUnavailableItemKey)) return;
                        context.HandleResponse();
                        await WriteKeysUnavailableAsync(context.HttpContext, context.HttpContext.RequestAborted);
                    }
                };
            });

        if (jwksEndpoint is not null)
        {
            services.AddHttpClient(JwksHttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

            services.AddSingleton(provider => new JwksConfigurationManager(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient(JwksHttpClientName),
                jwksEndpoint,
                issuer,
                provider.GetRequiredService<TimeProvider>()));

            services.AddOptions<JwtBearerOptions>(SchemeName)
                .Configure<JwksConfigurationManager>((options, manager) =>
                {
                    options.ConfigurationManager = manager;
                });
        }

        // No usable trusted key: fail closed (401) without terminating the host. The
        // warning is emitted at startup so operators can see the missing configuration.
        if (!hasTrustMaterial)
        {
            services.AddHostedService(provider => new MissingTrustConfigurationWarning(
                provider.GetRequiredService<ILoggerFactory>()));
        }

        return services;
    }

    private static bool IsInsecureTransportAllowed(IConfiguration configuration, Uri endpoint)
    {
        var configured = configuration.GetValue<bool?>("JwtValidation:AllowInsecureTransport");
        if (configured is { } value) return value;
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)) return true;
        return endpoint.IsLoopback || !endpoint.Host.Contains('.');
    }

    private static bool HasIdentity(ClaimsPrincipal? principal)
    {
        if (principal is null) return false;
        var subjects = principal.FindAll("sub").ToList();
        var accounts = principal.FindAll("account_id").ToList();
        var roles = principal.FindAll("role").ToList();
        return subjects.Count == 1 && Guid.TryParse(subjects[0].Value, out var subjectId) && subjectId != Guid.Empty &&
            accounts.Count == 1 && Guid.TryParse(accounts[0].Value, out var accountId) && accountId != Guid.Empty &&
            roles.Count == 1 && !string.IsNullOrWhiteSpace(roles[0].Value);
    }

    private static async Task WriteKeysUnavailableAsync(HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json";
        context.Response.Headers.TryAdd("WWW-Authenticate", "Bearer");
        var traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
        var json = JsonSerializer.Serialize(new
        {
            success = false,
            errorCode = KeysUnavailableCode,
            code = KeysUnavailableCode,
            message = "Authentication is temporarily unavailable.",
            traceId
        });
        await context.Response.WriteAsync(json, cancellationToken);
    }

    private static RsaSecurityKey? TryLoadPublicKey(string? publicKeyPem)
    {
        if (string.IsNullOrWhiteSpace(publicKeyPem)) return null;

        try
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            return new RsaSecurityKey(rsa);
        }
        catch (CryptographicException)
        {
            // Malformed key material must not crash startup; the scheme fails closed.
            return null;
        }
    }

    private sealed class MissingTrustConfigurationWarning : IHostedService
    {
        private readonly ILogger<MissingTrustConfigurationWarning> _logger;

        public MissingTrustConfigurationWarning(ILoggerFactory loggerFactory) =>
            _logger = loggerFactory.CreateLogger<MissingTrustConfigurationWarning>();

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning(
                "JwtValidation:JwksUri or JwtValidation:PublicKeyPem is not configured. " +
                "Admin payment integration calls will return 401 until a trusted UserService key is configured.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
