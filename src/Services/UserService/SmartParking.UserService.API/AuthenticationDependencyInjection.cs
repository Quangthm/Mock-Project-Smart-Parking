using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IdentityModel.Tokens.Jwt;
using MediatR;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Usecase.Session;
using UserService.Infrastructure.Services;
namespace UserService.API;
public static class AuthenticationDependencyInjection
{
    public static IServiceCollection AddJWTAuthentication(this IServiceCollection services, IConfiguration configuration,
        bool allowEphemeralKey = false)
    {
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new();
        services.AddSingleton(jwtOptions);
        services.AddScoped<IAccessTokenService, AccessTokenService>();
        services.AddSingleton(_ => new JwtKeyProvider(jwtOptions, allowEphemeralKey));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtKeyProvider, TimeProvider>((options, keys, clock) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = AccessTokenService.ValidationParameters(jwtOptions, keys, clock);
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.HttpContext.Request.Path.StartsWithSegments("/hubs/operators") &&
                            context.HttpContext.Request.Query.TryGetValue("access_token", out var token))
                            context.Token = token;
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var sessions = context.HttpContext.RequestServices.GetRequiredService<IAuthSessionStore>();
                        if (!Guid.TryParse(principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out var tokenId)
                            || !Guid.TryParse(principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
                            || !await sessions.IsActiveAsync(tokenId, userId, context.HttpContext.RequestAborted))
                        {
                            context.Fail("Session is invalid or revoked.");
                            return;
                        }
                        var mediator = context.HttpContext.RequestServices.GetRequiredService<IMediator>();
                        var user = await mediator.Send(new GetCurrentUserQuery(userId), context.HttpContext.RequestAborted);
                        if (user is null || user.Role != principal?.FindFirst("role")?.Value)
                            context.Fail("Account is inactive or permissions have changed.");
                    },
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        return context.Response.WriteAsJsonAsync(new
                        {
                            success = false, code = "INVALID_TOKEN", message = "Token is invalid, expired or revoked."
                        });
                    }
                };
            });
        return services;
    }
}
