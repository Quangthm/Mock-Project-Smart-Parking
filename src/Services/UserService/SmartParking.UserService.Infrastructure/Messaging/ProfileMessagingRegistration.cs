using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Profiles.Events;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Common.Interfaces.Services;
using UserService.Infrastructure.Services;

namespace UserService.Infrastructure.Messaging;

public static class ProfileMessagingRegistration
{
    public const string ProfileNameChangedExchange = "smartpark.user-access.user-profile-name-changed.v1";
    public const string ProfileAuditQueue = "smartpark.notification-audit.user-profile-name-changed.v1";
    public static IServiceCollection AddProfileMessaging(this IServiceCollection services,
        IConfiguration configuration, Action<IBusRegistrationConfigurator> configureOutbox,
        Action<IReceiveEndpointConfigurator, IBusRegistrationContext>? configureAccessEndpoint = null)
    {
        var accessEnabled = configuration.GetValue<bool>("Messaging:Enabled");
        var reservationEnabled = configuration.GetValue<bool>("ReservationAdmission:Enabled");
        if (reservationEnabled && (string.IsNullOrWhiteSpace(configuration["ReservationAdmission:BrokerUser"]) ||
            string.IsNullOrWhiteSpace(configuration["ReservationAdmission:BrokerPassword"])))
            throw new InvalidOperationException("Configure UserService restricted broker credentials for Account-current decisions.");
        if (reservationEnabled && configureAccessEndpoint is null)
            throw new InvalidOperationException("Account-current consumer requires its owning EF outbox.");
        services.AddScoped<UserService.Application.Authorization.IAccountAccessDecisionPublisher, AccountAccessDecisionPublisher>();
        if (accessEnabled && configureAccessEndpoint is null)
            throw new InvalidOperationException("Configure the access consumer with its owning EF outbox.");
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        services.AddMassTransit(registration =>
        {
            configureOutbox(registration);
            if (accessEnabled) registration.AddConsumer<OperatorAccessChangedConsumer>();
            if (reservationEnabled) registration.AddConsumer<AccountCurrentConsumer>();
            registration.UsingRabbitMq((context, bus) =>
            {
                // Host setup uses loopback/published port; app Compose uses broker DNS/internal port.
                bus.Host(configuration["SMARTPARK_RABBITMQ_HOST"] ?? "127.0.0.1",
                    configuration.GetValue<ushort?>("SMARTPARK_RABBITMQ_PORT") ?? 5672,
                    configuration["SMARTPARK_RABBITMQ_VHOST"] ?? "smartpark", host =>
                    {
                        host.Username(reservationEnabled ? configuration["ReservationAdmission:BrokerUser"]
                            ?? throw new InvalidOperationException("Configure the UserService restricted broker user.")
                            : configuration["SMARTPARK_RABBITMQ_USER"] ?? "smartpark");
                        host.Password((reservationEnabled ? configuration["ReservationAdmission:BrokerPassword"] : configuration["SMARTPARK_RABBITMQ_PASSWORD"])
                            ?? throw new InvalidOperationException("Configure SMARTPARK_RABBITMQ_PASSWORD."));
                    });
                bus.Message<UserProfileNameChanged>(message => message.SetEntityName(ProfileNameChangedExchange));
                bus.Publish<UserProfileNameChanged>(publish =>
                {
                    publish.Durable = true;
                    publish.AutoDelete = false;
                    publish.ExchangeType = "fanout";
                });
                // Profile audit routing is provisioned independently; no audit consumer is required.
                if (reservationEnabled)
                    bus.ReceiveEndpoint(SmartParking.Contracts.AccountAccess.AccountAccessEndpoints.Requests, endpoint =>
                    {
                        endpoint.UseMessageRetry(retry =>
                        {
                            retry.Handle<System.Data.Common.DbException>(error => error.IsTransient);
                            retry.Interval(1, TimeSpan.FromMilliseconds(250));
                        });
                        configureAccessEndpoint?.Invoke(endpoint, context);
                        endpoint.ConfigureConsumer<AccountCurrentConsumer>(context);
                    });
                if (accessEnabled)
                    bus.ReceiveEndpoint("smartpark-user-access-projection-v1", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Intervals(
                            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                        configureAccessEndpoint?.Invoke(endpoint, context);
                        endpoint.ConfigureConsumer<OperatorAccessChangedConsumer>(context);
                    });
            });
        });
        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = false;
            options.StopTimeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }
}
