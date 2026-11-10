using UserService.Application.Common.Models.JwT;
using UserService.Infrastructure.Services;

namespace UserService.API.Extensions;

public static class JwksEndpointExtensions
{
    public static IEndpointConventionBuilder MapUserServiceJwks(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<JwtOptions>();
        var keys = app.Services.GetRequiredService<JwtKeyProvider>();
        if (keys.IsEphemeral)
            app.Logger.LogWarning("UserService uses an ephemeral Development JWT key; access tokens become invalid after restart. Configure a stable private key and Jwt:KeyId for shared JWT development.");

        // Explicit public-only projection: never serialize the provider or a signing SecurityKey.
        var document = new
        {
            keys = new[] { new { kty = "RSA", kid = keys.KeyId, use = "sig", alg = "RS256", n = keys.PublicModulus, e = keys.PublicExponent } }
        };
        return app.MapGet(options.JwksPath, (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Json(document, contentType: "application/jwk-set+json");
        }).AllowAnonymous().WithName("UserServiceJwks");
    }
}
