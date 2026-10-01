using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using UserService.Application.Common.Behaviors;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Services;

namespace UserService.Application
{
    /// <summary>
    /// Application dependency injection
    /// </summary>
    public static class ApplicationDependencyInjection
    {
        /// <summary>
        /// Adds the application services.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>Application services.</returns>
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
            });
            services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
            services.AddAutoMapper(cfg =>
            {
                cfg.AddMaps(typeof(ApplicationDependencyInjection).Assembly);
            });

            // Add scope transient Pipeline Behavior
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            // Add scope for services
            services.AddScoped<IAccessTokenService, AccessTokenService>();

            return services;
        }

        /// <summary>
        /// Adds the JWT authentication.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>JWT Service</returns>
        public static IServiceCollection AddJWTAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var jwtOptions = new JwtOptions();
            configuration.GetSection("Jwt").Bind(jwtOptions);

            services.AddAuthentication(opt =>
            {
                opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
                 {
                     options.TokenValidationParameters = new TokenValidationParameters
                     {
                         ValidateIssuer = true,
                         ValidateAudience = true,
                         ValidateLifetime = true,
                         ValidateIssuerSigningKey = true,
                         ValidIssuer = jwtOptions.Issuer,
                         ValidAudience = jwtOptions.Audience,
                         IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
                     };
                 });

            return services;
        }
    }
}
