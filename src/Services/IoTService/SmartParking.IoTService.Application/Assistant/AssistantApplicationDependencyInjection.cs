using Microsoft.Extensions.DependencyInjection;

namespace SmartParking.IoTService.Application.Assistant;

public static class AssistantApplicationDependencyInjection
{
    public static IServiceCollection AddAssistantApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<GetAssistantCapabilitiesQuery>());
        return services;
    }
}
