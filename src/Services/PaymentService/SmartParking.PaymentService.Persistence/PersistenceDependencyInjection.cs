using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Persistence.Adapters;

namespace SmartParking.PaymentService.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        if (configuration != null)
        {
            var connectionString = configuration.GetConnectionString("Payment")
                ?? configuration.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddDbContext<PaymentDbContext>(options =>
                    options.UseNpgsql(connectionString));
            }
        }

        services.AddScoped<IParkingTariffPort, TariffPersistenceAdapter>();

        return services;
    }
}
