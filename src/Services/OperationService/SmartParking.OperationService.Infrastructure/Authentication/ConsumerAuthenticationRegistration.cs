using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace SmartParking.OperationService.Infrastructure.Authentication;

public static class ConsumerAuthenticationRegistration
{
    private const string DependencyFailure = "SmartParking.Authentication.KeysUnavailable";

    public static IServiceCollection AddConsumerAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var issuer = configuration["JwtValidation:Issuer"];
        var audience = configuration["JwtValidation:Audience"];
        var address = configuration["JwtValidation:JwksUri"];
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) ||
            !Uri.TryCreate(address, UriKind.Absolute, out var endpoint) || endpoint.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Configure JwtValidation issuer, audience and trusted JWKS URL.");
        if (endpoint.Scheme != "https" && !configuration.GetValue<bool>("JwtValidation:AllowInsecureTransport"))
            throw new InvalidOperationException("JWKS requires HTTPS unless protected/local HTTP is explicitly configured.");

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient("SmartParking.Jwks").ConfigurePrimaryHttpMessageHandler(() =>
            new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton(provider => new JwksConfigurationManager(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("SmartParking.Jwks"),
            endpoint, issuer, provider.GetRequiredService<TimeProvider>()));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwksConfigurationManager>((options, manager) =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.ConfigurationManager = manager;
                options.RefreshOnIssuerKeyNotFound = true;
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true, ValidIssuer = issuer,
                    ValidateAudience = true, ValidAudience = audience,
                    ValidateLifetime = true, RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub", RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (!HasIdentity(context.Principal)) context.Fail("Required identity claims are invalid.");
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        for (Exception? error = context.Exception; error is not null; error = error.InnerException)
                            if (error is JwksUnavailableException) context.HttpContext.Items[DependencyFailure] = true;
                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        var unavailable = context.HttpContext.Items.ContainsKey(DependencyFailure);
                        await WriteError(context.HttpContext, unavailable ? 503 : 401,
                            unavailable ? "AUTHENTICATION_KEYS_UNAVAILABLE" : "UNAUTHENTICATED",
                            unavailable ? "Authentication is temporarily unavailable." : "Authentication is required.");
                    },
                    OnForbidden = context => WriteError(context.HttpContext, 403, "FORBIDDEN", "Access is denied.")
                };
            });
        services.AddAuthorization();
        return services;
    }

    private static bool HasIdentity(ClaimsPrincipal? principal) => principal is not null &&
        HasGuid(principal, "sub") && HasGuid(principal, "account_id") &&
        principal.FindAll("role").Count() == 1 && !string.IsNullOrWhiteSpace(principal.FindFirst("role")?.Value);

    private static bool HasGuid(ClaimsPrincipal principal, string type) =>
        principal.FindAll(type).Count() == 1 && Guid.TryParse(principal.FindFirst(type)?.Value, out var id) && id != Guid.Empty;

    private static async Task WriteError(HttpContext context, int status, string code, string message)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        if (!HttpMethods.IsHead(context.Request.Method))
            await context.Response.WriteAsJsonAsync(new { success = false, code, message, traceId = context.TraceIdentifier }, context.RequestAborted);
    }
}
