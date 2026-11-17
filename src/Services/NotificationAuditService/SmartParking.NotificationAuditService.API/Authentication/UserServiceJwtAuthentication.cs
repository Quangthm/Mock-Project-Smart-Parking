using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace SmartParking.NotificationAuditService.API.Authentication;

/// <summary>
/// Configures UserService JWT bearer validation. Verification key material must be
/// supplied explicitly (JWKS endpoint, RSA public key PEM or symmetric key); there is
/// no default signing key, and the scheme fails closed when none is configured.
/// </summary>
public static class UserServiceJwtAuthentication
{
    private const string JwksClientName = "NotificationAuditService.Jwks";
    private const string DefaultIssuer = "SmartParking.UserService";
    private const string DefaultAudience = "SmartParking";
    // Shared with the OperationService challenge contract: key-dependency outage flag.
    private const string KeysUnavailableItemKey = "SmartParking.Authentication.KeysUnavailable";
    private const string KeysUnavailableCode = "AUTHENTICATION_KEYS_UNAVAILABLE";
    // Only the canonical issuer/audience defaults are trusted; no demo trust.
    private static readonly string[] DefaultIssuers = [DefaultIssuer];
    private static readonly string[] DefaultAudiences = [DefaultAudience];

    public static IServiceCollection AddUserServiceJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(JwksClientName).ConfigurePrimaryHttpMessageHandler(() =>
            new HttpClientHandler { AllowAutoRedirect = false });

        // Key material is resolved lazily when the scheme options are built so that
        // configuration sources added after startup (test hosts, late providers) apply.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IHttpClientFactory, TimeProvider, IConfiguration, ILoggerFactory>((options, httpClientFactory, clock, config, loggerFactory) =>
            {
                var jwtValidation = config.GetSection("JwtValidation");
                var jwt = config.GetSection("Jwt");

                var issuer = FirstNonEmpty(jwtValidation["Issuer"], jwt["Issuer"]) ?? DefaultIssuer;
                var audience = FirstNonEmpty(jwtValidation["Audience"], jwt["Audience"]) ?? DefaultAudience;
                var validIssuers = new[] { issuer }.Concat(DefaultIssuers).Distinct(StringComparer.Ordinal).ToArray();
                var validAudiences = new[] { audience }.Concat(DefaultAudiences).Distinct(StringComparer.Ordinal).ToArray();

                ApplyCommon(options, validIssuers, validAudiences);
                ApplyEvents(options);

                var jwksUri = FirstNonEmpty(jwtValidation["JwksUri"]);
                var publicKeyPem = FirstNonEmpty(jwtValidation["PublicKeyPem"], jwtValidation["PublicKey"], jwt["PublicKeyPem"], jwt["PublicKey"]);
                var secretKey = FirstNonEmpty(jwtValidation["SecretKey"], jwtValidation["Key"], jwt["SecretKey"], jwt["Key"]);

                if (Uri.TryCreate(jwksUri, UriKind.Absolute, out var endpoint) && endpoint.Scheme is "http" or "https")
                {
                    if (endpoint.Scheme != "https" && !IsInsecureTransportAllowed(config, endpoint))
                        throw new InvalidOperationException(
                            "JwtValidation:JwksUri requires HTTPS unless protected/local HTTP is explicitly " +
                            "configured with JwtValidation:AllowInsecureTransport=true.");
                    options.ConfigurationManager = new JwksConfigurationManager(
                        httpClientFactory.CreateClient(JwksClientName), endpoint, issuer, clock);
                    options.RefreshOnIssuerKeyNotFound = true;
                    options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
                }
                else if (!string.IsNullOrWhiteSpace(publicKeyPem))
                {
                    var rsa = RSA.Create();
                    rsa.ImportFromPem(publicKeyPem);
                    options.TokenValidationParameters.IssuerSigningKeys = [new RsaSecurityKey(rsa)];
                    options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
                }
                else if (!string.IsNullOrWhiteSpace(secretKey))
                {
                    options.TokenValidationParameters.IssuerSigningKeys = [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))];
                }
                else
                {
                    // Fail closed: no trusted key material is configured. Do not throw, so
                    // test hosts that build the app with minimal configuration still start.
                    loggerFactory.CreateLogger("SmartParking.NotificationAuditService.Authentication").LogWarning(
                        "No UserService JWT key material configured (JwtValidation:JwksUri, JwtValidation:PublicKeyPem or JwtValidation:SecretKey). " +
                        "Protected endpoints will return 401 until key material is configured.");
                }
            });

        return services;
    }

    private static void ApplyCommon(JwtBearerOptions options, string[] issuers, string[] audiences)
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = issuers,
            ValidateAudience = true,
            ValidAudiences = audiences,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    }

    private static void ApplyEvents(JwtBearerOptions options)
    {
        options.Events = new JwtBearerEvents
        {
            // Core actor claims are mandatory: a non-blank user id (sub) and a
            // non-blank role. Anything else is rejected as an invalid token.
            OnTokenValidated = context =>
            {
                if (!HasIdentity(context.Principal, context.Request.Path.StartsWithSegments("/api/v1/admin")))
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
    }

    private static bool IsInsecureTransportAllowed(IConfiguration configuration, Uri endpoint)
    {
        var configured = configuration.GetValue<bool?>("JwtValidation:AllowInsecureTransport");
        if (configured is { } value) return value;
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)) return true;
        return endpoint.IsLoopback || !endpoint.Host.Contains('.');
    }

    private static bool HasIdentity(ClaimsPrincipal? principal, bool requireAdminActor)
    {
        if (principal is null) return false;
        var subjects = principal.FindAll("sub").ToList();
        // Existing notification routes retain their established token contract.
        // The new platform configuration route requires the full account-bound actor.
        if (!requireAdminActor)
            return subjects.Count == 1 && !string.IsNullOrWhiteSpace(subjects[0].Value) &&
                principal.FindAll("role").Any(claim => !string.IsNullOrWhiteSpace(claim.Value));
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

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
