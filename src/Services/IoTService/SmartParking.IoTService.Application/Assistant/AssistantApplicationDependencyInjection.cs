using Microsoft.Extensions.DependencyInjection;

namespace SmartParking.IoTService.Application.Assistant;

public static class AssistantApplicationDependencyInjection
{
    public static IServiceCollection AddAssistantApplication(this IServiceCollection services, bool includeDemo = true)
    {
        services.AddMediatR(configuration =>
        {
            configuration.TypeEvaluator = type => includeDemo || type.Namespace != typeof(Demo.GetDemoAssistantStatusHandler).Namespace;
            configuration.RegisterServicesFromAssemblyContaining<GetAssistantCapabilitiesQuery>();
        });
        return services;
    }
}
