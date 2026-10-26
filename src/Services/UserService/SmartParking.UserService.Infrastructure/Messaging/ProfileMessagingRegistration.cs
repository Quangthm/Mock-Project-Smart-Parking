using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Profiles.Events;

namespace UserService.Infrastructure.Messaging;

public static class ProfileMessagingRegistration
{
    public const string ProfileNameChangedExchange = "smartpark.user-access.user-profile-name-changed.v1";
    public const string ProfileAuditQueue = "smartpark.notification-audit.user-profile-name-changed.v1";

    /// <summary>The composition root supplies Persistence's EF Bus Outbox registration.</summary>
    public static IServiceCollection AddProfileMessaging(this IServiceCollection services,
        IConfiguration configuration, Action<IBusRegistrationConfigurator> configureOutbox)
    {
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        services.AddMassTransit(registration =>
        {
            configureOutbox(registration);
            registration.UsingRabbitMq((_, bus) =>
            {
                var broker = configuration.GetSection("Messaging:RabbitMq");
                bus.Host(broker["Host"] ?? "localhost", broker.GetValue<ushort?>("Port") ?? 5672,
                    broker["VirtualHost"] ?? "/", host =>
                    {
                        host.Username(broker["Username"] ?? "guest");
                        host.Password(broker["Password"] ?? "guest");
                    });
                bus.Message<UserProfileNameChanged>(message => message.SetEntityName(ProfileNameChangedExchange));
                bus.Publish<UserProfileNameChanged>(publish =>
                {
                    publish.Durable = true;
                    publish.AutoDelete = false;
                    publish.ExchangeType = "fanout";
                });
                // No receive endpoint: durable routing is provisioned independently before delivery is enabled.
            });
        });
        services.Configure<MassTransitHostOptions>(options =>
        {
            // API startup and database capture stay available while the broker is unavailable.
            options.WaitUntilStarted = false;
            options.StopTimeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }
}
