using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.ReservationService.Application.Common.Interfaces.Services;
using SmartParking.ReservationService.Infrastructure.Clients;
using SmartParking.ReservationService.Persistence;

namespace SmartParking.ReservationService.Infrastructure;

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
        services.AddHttpClient<ParkingGateSlotClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:Parking"] ?? "http://localhost:5045/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey);
        });
        services.AddScoped<IOperatorGateSlotClaims>(provider =>
            provider.GetRequiredService<ParkingGateSlotClient>());

        return services;
    }
}
