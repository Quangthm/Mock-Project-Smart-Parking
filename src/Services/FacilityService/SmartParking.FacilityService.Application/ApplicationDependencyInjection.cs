using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.FacilityService.Application.Common.Behaviors;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });
        services.AddScoped<IOwnerScopeResolver, OwnerScopeResolver>();
        return services;
    }
}
