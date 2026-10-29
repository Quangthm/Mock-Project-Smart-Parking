using Microsoft.AspNetCore.Authentication.JwtBearer;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Services;
using UserService.API.Services;

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
        services.AddScoped<CurrentAccountBearerEvents>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtKeyProvider>((options, keys) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = AccessTokenService.ValidationParameters(jwtOptions, keys);
                options.EventsType = typeof(CurrentAccountBearerEvents);
            });
        return services;
    }
}
