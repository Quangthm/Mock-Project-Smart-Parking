using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartParking.FacilityService.Infrastructure;

public static class ReservationSlotMessaging
{
    public static void AddReservationSlotMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("ReservationAdmission:SagaConsumersEnabled")) return;
        var user = configuration["ReservationAdmission:BrokerUser"];
        var password = configuration["ReservationAdmission:BrokerPassword"];
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Configure restricted Facility broker credentials.");
        services.AddMassTransit(registration =>
        {
            registration.AddConsumer<ReservationSlotConsumer>();
            registration.UsingRabbitMq((context, bus) =>
            {
                bus.Host(configuration["SMARTPARK_RABBITMQ_HOST"] ?? "127.0.0.1",
                    configuration.GetValue<ushort?>("SMARTPARK_RABBITMQ_PORT") ?? 5672,
                    configuration["SMARTPARK_RABBITMQ_VHOST"] ?? "smartpark", host =>
                    { host.Username(user); host.Password(password); });
                bus.ReceiveEndpoint("facility-reservation-slot-v1", endpoint =>
                {
                    endpoint.ConfigureConsumeTopology = false;
                    endpoint.UseMessageRetry(r => r.Interval(2, TimeSpan.FromMilliseconds(250)));
                    endpoint.UseInMemoryOutbox(context);
                    endpoint.ConfigureConsumer<ReservationSlotConsumer>(context);
                });
            });
        });
    }
}
