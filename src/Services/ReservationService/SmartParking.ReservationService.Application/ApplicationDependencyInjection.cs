using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace SmartParking.ReservationService.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly));
        services.AddScoped<StructureSafetyService>();
        return services;
    }
}
