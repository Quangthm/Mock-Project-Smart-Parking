using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Infrastructure.Clients;
using SmartParking.OperationService.Infrastructure.Messaging;
using MassTransit;
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
        services.AddHttpClient<ReservationFacilityClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:Facility"] ?? configuration["Services:Parking"] ?? "http://localhost:5045/");
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IReservationFacilityOwner>(p => p.GetRequiredService<ReservationFacilityClient>());
        services.AddScoped<IReservationFacilityDependencyOwner>(p => p.GetRequiredService<ReservationFacilityClient>());
        services.AddHttpClient<ParkingGateSlotClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:Parking"]
                ?? configuration["Services:Facility"]
                ?? "http://localhost:5045/");
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

        if (configuration.GetValue<bool>("Messaging:Enabled"))
        {
            services.AddMassTransit(registration =>
            {
                registration.AddConsumer<PaymentSucceededConsumer>();
                registration.UsingRabbitMq((context, bus) =>
                {
                    bus.Message<PaymentSucceededV1>(message =>
                        message.SetEntityName("smartpark.payment.payment-succeeded.v1"));
                    bus.Host(
                        configuration["SMARTPARK_RABBITMQ_HOST"] ?? "127.0.0.1",
                        configuration.GetValue<ushort?>("SMARTPARK_RABBITMQ_PORT") ?? 5672,
                        configuration["SMARTPARK_RABBITMQ_VHOST"] ?? "smartpark",
                        host =>
                        {
                            host.Username(configuration["SMARTPARK_RABBITMQ_USER"] ?? "smartpark");
                            host.Password(configuration["SMARTPARK_RABBITMQ_PASSWORD"]
                                ?? throw new InvalidOperationException("Configure SMARTPARK_RABBITMQ_PASSWORD."));
                        });
                    bus.ReceiveEndpoint("smartpark-operation-payment-succeeded-v1", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Intervals(
                            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                        endpoint.ConfigureConsumer<PaymentSucceededConsumer>(context);
                    });
                });
            });
        }

        return services;
    }
}
