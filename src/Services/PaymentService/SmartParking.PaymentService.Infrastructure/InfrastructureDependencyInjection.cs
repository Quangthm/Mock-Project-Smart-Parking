using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Domain.Ports;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;

namespace SmartParking.PaymentService.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var reservationServiceBaseUrl = configuration["Services:OperationService:BaseUrl"] ?? "http://localhost:5002/";

        services.Configure<PayOsOptions>(configuration.GetSection(PayOsOptions.SectionName));

        services.AddHttpClient<IReservationPricingSnapshotPort, ReservationSnapshotHttpAdapter>(client =>
        {
            client.BaseAddress = new Uri(reservationServiceBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        services.AddHttpClient<IPaymentGatewayPort, PayOsAdapter>((serviceProvider, client) =>
        {
            var options = configuration.GetSection(PayOsOptions.SectionName).Get<PayOsOptions>() ?? new PayOsOptions();
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://api-merchant.payos.vn" : options.BaseUrl;
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
