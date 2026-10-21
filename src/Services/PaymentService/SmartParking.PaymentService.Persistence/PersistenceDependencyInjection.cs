using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Persistence.Context;
using SmartParking.PaymentService.Persistence.Repositories;

namespace SmartParking.PaymentService.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddDbContext<PaymentDbContext>((serviceProvider, options) =>
        {
            var config = configuration ?? serviceProvider.GetService<IConfiguration>();
            var connectionString = config?.GetConnectionString("Payment")
                                   ?? config?.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Fallback for local testing or schema integration container port 5444
                connectionString = "Host=127.0.0.1;Port=5444;Database=smartpark_payment;Username=smartpark_payment;Password=smartpark_dev_password";
            }

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IPaymentOrderRepository, PaymentOrderRepository>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<IProviderWebhookRepository, ProviderWebhookRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
