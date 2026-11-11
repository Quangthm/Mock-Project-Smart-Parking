using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartParking.NotificationAuditService.Persistence;

public static class PersistenceServiceRegistration
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        return services.AddPersistenceServices(configuration);
    }
}
