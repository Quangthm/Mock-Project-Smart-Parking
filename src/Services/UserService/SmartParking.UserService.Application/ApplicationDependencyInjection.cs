using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using UserService.Application.Usecase.Session;
using UserService.Application.Common.Behaviors;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Services;
using UserService.Application.Usecase.ValidateAccessToken;

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
                // The gRPC sample has no implementation yet; keep it out of this demo.
                cfg.TypeEvaluator = type => type != typeof(ValidateAccessTokenCommandHandler);
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
                     options.MapInboundClaims = false;
                     options.TokenValidationParameters = AccessTokenService.ValidationParameters(jwtOptions);
                     options.Events = new JwtBearerEvents
                     {
                         OnTokenValidated = async context =>
                         {
                             var principal = context.Principal;
                             var sessionId = principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                             var sessions = context.HttpContext.RequestServices.GetRequiredService<IAuthSessionStore>();
                             if (string.IsNullOrEmpty(sessionId)
                                 || !Guid.TryParse(principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
                                 || !sessions.IsActive(sessionId, userId))
                             {
                                 context.Fail("Session is invalid or revoked.");
                                 return;
                             }
                             var mediator = context.HttpContext.RequestServices.GetRequiredService<IMediator>();
                             var user = await mediator.Send(new GetCurrentUserQuery(userId), context.HttpContext.RequestAborted);
                             if (user is null || user.Role != principal?.FindFirst("role")?.Value)
                                 context.Fail("Account is inactive or permissions have changed.");
                         }
                     };
                 });

            return services;
        }
    }
}
