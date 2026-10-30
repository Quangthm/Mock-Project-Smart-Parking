using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Persistence.Adapters;
using SmartParking.PaymentService.Persistence.Repositories;

namespace SmartParking.PaymentService.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddDbContext<PaymentDbContext>((serviceProvider, options) =>
        {
            var config = configuration ?? serviceProvider.GetService<IConfiguration>();
            var connectionString = config?.GetConnectionString("Payment")
                                   ?? config?.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Fallback matching 229 database configuration
                connectionString = "Host=localhost;Port=5444;Database=smartpark_payment;Username=postgres;Password=postgres";
            }

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IParkingTariffPort, TariffPersistenceAdapter>();
        services.AddScoped<IPaymentOrderRepository, PaymentOrderRepository>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<IProviderWebhookRepository, ProviderWebhookRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
