using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace SmartParking.OperationService.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly));
        services.AddScoped<StructureSafetyService>();
        return services;
    }
}
