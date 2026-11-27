using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Persistence;

namespace SmartParking.FacilityService.Infrastructure.Messaging;

public static class FacilityMessaging
{
    public static IServiceCollection AddFacilityMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var outboxEnabled = configuration.GetValue<bool>("Messaging:LayoutOutboxEnabled");
        var sagaEnabled = configuration.GetValue<bool>("ReservationAdmission:SagaConsumersEnabled");
        if (outboxEnabled)
        {
            services.AddScoped<ILayoutInvalidationPublisher, MassTransitLayoutInvalidationPublisher>();
            services.AddScoped<IFacilityCapacityRestrictionPublisher, MassTransitFacilityCapacityRestrictionPublisher>();
            services.AddScoped<IFacilityMaintenancePublisher, MassTransitFacilityMaintenancePublisher>();
        }
        else
        {
            services.AddScoped<ILayoutInvalidationPublisher, NoOpLayoutInvalidationPublisher>();
            services.AddScoped<IFacilityCapacityRestrictionPublisher, NoOpFacilityCapacityRestrictionPublisher>();
            services.AddScoped<IFacilityMaintenancePublisher, NoOpFacilityMaintenancePublisher>();
        }

        if (!outboxEnabled && !sagaEnabled)
            return services;

        var restrictedUser = configuration["ReservationAdmission:BrokerUser"];
        var restrictedPassword = configuration["ReservationAdmission:BrokerPassword"];
        if (sagaEnabled && (string.IsNullOrWhiteSpace(restrictedUser) || string.IsNullOrWhiteSpace(restrictedPassword)))
            throw new InvalidOperationException("Configure restricted Facility broker credentials.");

        var amqp = configuration["RabbitMq:ConnectionString"]
            ?? configuration.GetConnectionString("RabbitMq");
        var hasUri = !sagaEnabled && !string.IsNullOrWhiteSpace(amqp) && Uri.TryCreate(amqp, UriKind.Absolute, out _);
        var hostName = configuration["SMARTPARK_RABBITMQ_HOST"] ?? "127.0.0.1";
        var port = configuration.GetValue<ushort?>("SMARTPARK_RABBITMQ_PORT") ?? 5672;
        var vhost = configuration["SMARTPARK_RABBITMQ_VHOST"] ?? "smartpark";
        var user = sagaEnabled ? restrictedUser! : (configuration["SMARTPARK_RABBITMQ_USER"] ?? "smartpark");
        var password = sagaEnabled ? restrictedPassword : configuration["SMARTPARK_RABBITMQ_PASSWORD"];

        if (!hasUri && string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Configure RabbitMq:ConnectionString or SMARTPARK_RABBITMQ_PASSWORD when Messaging:LayoutOutboxEnabled is true.");

        services.AddMassTransit(registration =>
        {
            if (sagaEnabled)
                registration.AddConsumer<ReservationSlotConsumer>();

            if (outboxEnabled)
            {
                registration.AddEntityFrameworkOutbox<ParkingDbContext>(outbox =>
                {
                    outbox.UsePostgres();
                    outbox.QueryDelay = TimeSpan.FromSeconds(1);
                    outbox.UseBusOutbox(bus =>
                    {
                        if (!configuration.GetValue("Messaging:OutboxDeliveryEnabled", true))
                            bus.DisableDeliveryService();
                    });
                });
            }

            registration.UsingRabbitMq((context, bus) =>
            {
                if (hasUri)
                {
                    bus.Host(UriFromAmqp(amqp!));
                }
                else
                {
                    bus.Host(hostName, port, vhost, host =>
                    {
                        host.Username(user);
                        host.Password(password!);
                    });
                }

                if (sagaEnabled)
                {
                    bus.ReceiveEndpoint("facility-reservation-slot-v1", endpoint =>
                    {
                        endpoint.ConfigureConsumeTopology = false;
                        endpoint.UseMessageRetry(r => r.Interval(2, TimeSpan.FromMilliseconds(250)));
                        endpoint.UseInMemoryOutbox(context);
                        endpoint.ConfigureConsumer<ReservationSlotConsumer>(context);
                    });
                }

                if (outboxEnabled)
                {
                    bus.Message<LayoutInvalidatedV1>(message =>
                        message.SetEntityName(LayoutInvalidationResources.ExchangeName));
                    bus.Publish<LayoutInvalidatedV1>(publish =>
                    {
                        publish.Durable = true;
                        publish.AutoDelete = false;
                        publish.ExchangeType = "fanout";
                    });

                    // Emergency restriction fanout exchanges consumed by OperationService.
                    bus.Message<FacilityCapacityRestrictedV1>(message =>
                        message.SetEntityName(FacilityCapacityRestrictionTopology.RestrictedExchangeName));
                    bus.Publish<FacilityCapacityRestrictedV1>(publish =>
                    {
                        publish.Durable = true;
                        publish.AutoDelete = false;
                        publish.ExchangeType = "fanout";
                    });
                    bus.Message<FacilityCapacityRestrictionReleasedV1>(message =>
                        message.SetEntityName(FacilityCapacityRestrictionTopology.ReleasedExchangeName));
                    bus.Publish<FacilityCapacityRestrictionReleasedV1>(publish =>
                    {
                        publish.Durable = true;
                        publish.AutoDelete = false;
                        publish.ExchangeType = "fanout";
                    });

                    // Authoritative scheduled maintenance change events consumed by OperationService.
                    bus.Message<FacilityMaintenanceChangedV1>(message =>
                        message.SetEntityName(FacilityMaintenanceTopology.ChangedExchangeName));
                    bus.Publish<FacilityMaintenanceChangedV1>(publish =>
                    {
                        publish.Durable = true;
                        publish.AutoDelete = false;
                        publish.ExchangeType = "fanout";
                    });
                }
            });
        });

        return services;
    }

    private static Uri UriFromAmqp(string connectionString) => new(connectionString);
}
