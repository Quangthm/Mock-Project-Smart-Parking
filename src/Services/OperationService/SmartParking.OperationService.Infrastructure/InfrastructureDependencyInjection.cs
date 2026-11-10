using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Infrastructure.Clients;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SmartParking.OperationService.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistenceServices(configuration);
        services.AddHttpContextAccessor();

        services.AddHttpClient<OperatorIdentityClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:User"] ?? "http://localhost:5035/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IOperatorIdentityClient>(provider =>
            provider.GetRequiredService<OperatorIdentityClient>());

        var serviceKey = configuration["Services:Key"]
            ?? throw new InvalidOperationException("Configure Services:Key.");
        services.AddHttpClient<IReservationVehicleOwner, ReservationVehicleClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:User"] ?? "http://localhost:5035/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey);
        });
        services.TryAddScoped<IReservationCommercialOwner, UnavailableReservationCommercialOwner>();
        services.TryAddScoped<IReservationFacilityOwner, UnavailableReservationFacilityOwner>();
        services.TryAddScoped<IReservationFacilityDependencyOwner, UnavailableReservationFacilityDependencyOwner>();
        services.TryAddScoped<IReservationCapacityAcceptance, UnavailableReservationCapacityAcceptance>();
        services.AddHttpClient<ParkingGateSlotClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:Facility"] ?? "http://localhost:5045/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey);
        });
        services.AddScoped<IOperatorGateSlotClaims>(provider =>
            provider.GetRequiredService<ParkingGateSlotClient>());

        services.AddHttpClient<CheckoutPaymentClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:Payment"] ?? "http://localhost:5065/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<ICheckoutPaymentClient>(provider =>
            provider.GetRequiredService<CheckoutPaymentClient>());

        return services;
    }
}
