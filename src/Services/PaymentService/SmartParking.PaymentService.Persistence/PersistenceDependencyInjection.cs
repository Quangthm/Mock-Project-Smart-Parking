using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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

            var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(connectionBuilder.Username) ||
                connectionBuilder.Username.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            {
                connectionBuilder.Username = "smartpark_payment";
            }

            var developmentPassword = Environment.GetEnvironmentVariable("SMARTPARK_DEV_PASSWORD");
            if (string.IsNullOrWhiteSpace(connectionBuilder.Password) ||
                connectionBuilder.Password.Contains('*') ||
                connectionBuilder.Password.Equals("replace-me", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(developmentPassword))
                {
                    throw new InvalidOperationException(
                        "Payment database password is not configured. Set SMARTPARK_DEV_PASSWORD or ConnectionStrings__Payment.");
                }

                connectionBuilder.Password = developmentPassword;
            }

            options.UseNpgsql(connectionBuilder.ConnectionString);
        });

        services.AddScoped<IParkingTariffPort, TariffPersistenceAdapter>();
        services.AddScoped<IPaymentOrderRepository, PaymentOrderRepository>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<IProviderWebhookRepository, ProviderWebhookRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
