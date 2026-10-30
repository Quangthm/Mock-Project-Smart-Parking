using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Common.Behaviors;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Services;
using UserService.Application.Usecase.Session;
using UserService.Application.Usecase.ValidateAccessToken;

namespace UserService.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, AuthenticationPolicy policy)
    {
        services.AddMediatR(cfg =>
        {
            cfg.TypeEvaluator = type => type != typeof(ValidateAccessTokenCommandHandler);
            cfg.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
        });
        services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(ApplicationDependencyInjection).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<AuthSessionService>();
        services.AddScoped<IOperatorInvitationFactory, OperatorInvitationFactory>();
        services.AddScoped<IVehicleRegistration, VehicleRegistrationService>();
        if (policy.MaxFailedLoginAttempts < 1 || policy.LockoutMinutes < 1 || policy.RefreshTokenDays < 1)
            throw new InvalidOperationException("Authentication policy values must be positive.");
        services.AddSingleton(policy);
        return services;
    }

    public static IServiceCollection AddIdentityWorkflowServices(this IServiceCollection services)
    {
        services.AddScoped<IDriverRegistrationService, DriverRegistrationService>();
        services.AddScoped<DriverRegistrationService>();
        services.AddScoped<IOwnerRegistrationService, OwnerRegistrationService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<AccountWorkflowService>();
        services.AddScoped<IAccountWorkflows>(sp=>sp.GetRequiredService<AccountWorkflowService>());
        services.AddScoped<IMfaPolicy>(sp=>sp.GetRequiredService<AccountWorkflowService>());
        return services;
    }

}
