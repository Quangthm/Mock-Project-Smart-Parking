using Microsoft.Extensions.DependencyInjection;
using SmartParking.IoTService.Application.Assistant;
namespace SmartParking.IoTService.Infrastructure.Assistant;
public static class AssistantDependencyInjection
{
    public static IServiceCollection AddAssistant(this IServiceCollection services, ChatbotOptions options)
    {
        if (options.TimeoutSeconds is < 1 or > 120 || options.AllowedIntents is null || options.AllowedIntents.Any(i => i is not ("FAQ" or "PARKING_SEARCH")))
            throw new InvalidOperationException("Invalid assistant timeout or read intent allowlist.");
        services.AddSingleton(options);
        services.AddScoped<IChatModel, UnconfiguredChatModel>();
        services.AddScoped<IChatBackendGateway, UnconfiguredChatBackend>();
        services.AddScoped<ChatbotCoordinator>();
        return services;
    }
}
