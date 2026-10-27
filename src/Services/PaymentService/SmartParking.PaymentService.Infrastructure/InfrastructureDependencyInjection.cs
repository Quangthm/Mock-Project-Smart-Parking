using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;

namespace SmartParking.PaymentService.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var reservationServiceBaseUrl = configuration["Services:ReservationService:BaseUrl"] ?? "http://localhost:5002/";

        services.AddHttpClient<IReservationPricingSnapshotPort, ReservationSnapshotHttpAdapter>(client =>
        {
            client.BaseAddress = new Uri(reservationServiceBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        return services;
    }
}
