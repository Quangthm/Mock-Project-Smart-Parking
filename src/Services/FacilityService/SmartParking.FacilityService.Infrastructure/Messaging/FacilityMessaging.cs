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
        if (!outboxEnabled)
        {
            services.AddScoped<ILayoutInvalidationPublisher, NoOpLayoutInvalidationPublisher>();
            services.AddScoped<IFacilityCapacityRestrictionPublisher, NoOpFacilityCapacityRestrictionPublisher>();
            services.AddScoped<IFacilityMaintenancePublisher, NoOpFacilityMaintenancePublisher>();
            return services;
        }

        var amqp = configuration["RabbitMq:ConnectionString"]
            ?? configuration.GetConnectionString("RabbitMq");
        var hasUri = !string.IsNullOrWhiteSpace(amqp) && Uri.TryCreate(amqp, UriKind.Absolute, out var amqpUri);
        var hostName = configuration["SMARTPARK_RABBITMQ_HOST"] ?? "127.0.0.1";
        var port = configuration.GetValue<ushort?>("SMARTPARK_RABBITMQ_PORT") ?? 5672;
        var vhost = configuration["SMARTPARK_RABBITMQ_VHOST"] ?? "smartpark";
        var user = configuration["SMARTPARK_RABBITMQ_USER"] ?? "smartpark";
        var password = configuration["SMARTPARK_RABBITMQ_PASSWORD"];

        if (!hasUri && string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Configure RabbitMq:ConnectionString or SMARTPARK_RABBITMQ_PASSWORD when Messaging:LayoutOutboxEnabled is true.");

        services.AddScoped<ILayoutInvalidationPublisher, MassTransitLayoutInvalidationPublisher>();
        services.AddScoped<IFacilityCapacityRestrictionPublisher, MassTransitFacilityCapacityRestrictionPublisher>();
        services.AddScoped<IFacilityMaintenancePublisher, MassTransitFacilityMaintenancePublisher>();
        services.AddMassTransit(registration =>
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

            registration.UsingRabbitMq((_, bus) =>
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
            });
        });

        return services;
    }

    private static Uri UriFromAmqp(string connectionString) => new(connectionString);
}
