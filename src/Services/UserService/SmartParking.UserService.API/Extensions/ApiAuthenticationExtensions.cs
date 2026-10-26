using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Profiles;
using UserService.Application.Services;

namespace UserService.API.Extensions;

/// <summary>Composes bearer authentication and adapts validated claims to Application policy.</summary>
public static class ApiAuthenticationExtensions
{
    public static IServiceCollection AddJWTAuthentication(this IServiceCollection services, IConfiguration configuration,
        bool allowEphemeralKey = false)
    {
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new();
        services.AddSingleton(jwtOptions);
        services.AddSingleton(_ => new JwtKeyProvider(jwtOptions, allowEphemeralKey));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtKeyProvider>((options, keys) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = AccessTokenService.ValidationParameters(jwtOptions, keys);
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var sessions = context.HttpContext.RequestServices.GetRequiredService<IAuthSessionStore>();
                        if (!Guid.TryParse(principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out var tokenId)
                            || !Guid.TryParse(principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
                            || !Guid.TryParse(principal?.FindFirst("account_id")?.Value, out var accountId)
                            || !await sessions.IsActiveAsync(tokenId, userId, accountId, context.HttpContext.RequestAborted))
                        {
                            context.Fail("Session is invalid or revoked.");
                            return;
                        }
                        var access = context.HttpContext.RequestServices.GetRequiredService<IProfileAccessStore>();
                        var clock = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
                        var state = await access.LoadCurrentAsync(accountId, tokenId, context.HttpContext.RequestAborted);
                        try
                        {
                            OwnProfileAccess.RequireCurrentAccess(state,
                                new TokenActor(userId, accountId, tokenId, principal?.FindFirst("role")?.Value), clock);
                        }
                        catch (AuthException) { context.Fail("Account access has changed."); }
                    }
                    // API error registration chains challenge/forbid callbacks separately.
                };
            });
        return services;
    }

    private sealed record TokenActor(Guid? UserId, Guid? AccountId, Guid? AccessTokenId, string? Role) : ICurrentActor;
}
