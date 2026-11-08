using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartParking.IoTService.API.Http;
using SmartParking.IoTService.Application.Assistant.Authorization;

namespace SmartParking.IoTService.API.Authentication;

public static class AssistantAuthentication
{
    // The composition root must supply trusted configuration. Its startup/unhealthy policy is B-02, pending approval.
    public static IServiceCollection AddAssistantAuthentication(this IServiceCollection services,
        string issuer, string audience, string publicKeyPem, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            throw new ArgumentException("Assistant JWT issuer and audience are required.");
        var key = ReadPublicKey(publicKeyPem);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpCurrentActor>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.SaveToken = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidIssuer = issuer,
                ValidateAudience = true, ValidAudience = audience, IgnoreTrailingSlashWhenValidatingAudience = false,
                ValidateIssuerSigningKey = true, IssuerSigningKey = key,
                RequireSignedTokens = true, ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero,
                NameClaimType = "sub", RoleClaimType = "role",
                LifetimeValidator = (notBefore, expires, _, _) =>
                {
                    var now = clock.GetUtcNow().UtcDateTime;
                    return expires.HasValue && expires > now &&
                        (!notBefore.HasValue || (notBefore <= now && notBefore < expires));
                }
            };
            options.Events = new()
            {
                OnTokenValidated = context =>
                {
                    var payload = context.SecurityToken switch
                    {
                        JsonWebToken token => token.EncodedPayload,
                        JwtSecurityToken token => token.RawPayload,
                        _ => ""
                    };
                    var actor = context.Principal is null ? null : AssistantTokenClaims.Read(context.Principal, payload);
                    if (actor is null) context.Fail("Invalid identity claims.");
                    else context.HttpContext.Items[HttpCurrentActor.ItemKey] = actor;
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    return AssistantErrors.WriteAsync(context.HttpContext, AssistantErrors.ForStatus(401));
                },
                OnForbidden = context => AssistantErrors.WriteAsync(context.HttpContext, AssistantErrors.ForStatus(403))
            };
        });
        services.AddAuthorization(options => options.AddPolicy("AssistantDriver", policy =>
            policy.RequireAuthenticatedUser().RequireRole("driver")));
        return services;
    }

    private static RsaSecurityKey ReadPublicKey(string pem)
    {
        // ImportFromPem also accepts private keys; reject them rather than importing signing material into IoT.
        if (string.IsNullOrWhiteSpace(pem) ||
            !PemEncoding.TryFind(pem, out var fields) ||
            pem[fields.Label] is not ("PUBLIC KEY" or "RSA PUBLIC KEY") ||
            !string.IsNullOrWhiteSpace(pem[..fields.Location.Start.Value]) ||
            !string.IsNullOrWhiteSpace(pem[fields.Location.End.Value..]))
            throw new ArgumentException("Exactly one RSA public PEM is required.");
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048) throw new ArgumentException("RSA public key must be at least 2048 bits.");
            return new RsaSecurityKey(rsa.ExportParameters(false));
        }
        catch (CryptographicException)
        {
            throw new ArgumentException("A valid RSA public PEM is required.");
        }
    }
}
