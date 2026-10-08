using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using UserService.Application.Common.Behaviors;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Services;
using UserService.Application.Usecase.Session;
using UserService.Application.Usecase.ValidateAccessToken;

namespace UserService.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
        {
            cfg.TypeEvaluator = type => type != typeof(ValidateAccessTokenCommandHandler);
            cfg.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
        });
        services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(ApplicationDependencyInjection).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IAccessTokenService, AccessTokenService>();
        services.AddScoped<AuthSessionService>();
        var policy = configuration.GetSection("AuthenticationPolicy").Get<AuthenticationPolicy>() ?? new();
        if (policy.MaxFailedLoginAttempts < 1 || policy.LockoutMinutes < 1 || policy.RefreshTokenDays < 1)
            throw new InvalidOperationException("Authentication policy values must be positive.");
        services.AddSingleton(policy);
        return services;
    }

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
