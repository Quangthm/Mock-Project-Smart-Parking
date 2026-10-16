using Microsoft.Extensions.DependencyInjection;

namespace SmartParking.PaymentService.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        // Persistence skeleton placeholder for SPARK-229.
        // Full DbContext and repositories will be registered here in the DB/Payment Order Sprint.
        return services;
    }
}
