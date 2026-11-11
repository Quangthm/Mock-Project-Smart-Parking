using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Infrastructure.Adapters.Delivery;
using SmartParking.NotificationAuditService.Infrastructure.Consumers;
using SmartParking.NotificationAuditService.Infrastructure.Options;
using SmartParking.PaymentService.Domain.Events;

namespace SmartParking.NotificationAuditService.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        // Delivery Adapters (Local Fixtures) & Dispatcher
        services.AddScoped<INotificationChannelAdapter, InAppDeliveryAdapter>();
        services.AddScoped<INotificationChannelAdapter, LocalEmailCaptureAdapter>();
        services.AddScoped<INotificationChannelAdapter, LocalSmsFixtureAdapter>();
        services.AddScoped<INotificationChannelAdapter, LocalPushFixtureAdapter>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        var useInMemory = configuration.GetValue<bool>("RabbitMQ:UseInMemory");

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddConsumer<AuditProjectionConsumer>();
            x.AddConsumer<ReservationNotificationConsumer>();
            x.AddConsumer<PaymentNotificationConsumer>();

            if (useInMemory)
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ReceiveEndpoint("notification-audit-service.payment-succeeded", e =>
                    {
                        e.PrefetchCount = 32;
                        e.ConcurrentMessageLimit = 16;
                        e.UseMessageRetry(r => r.Intervals(
                            TimeSpan.FromSeconds(2),
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromSeconds(10)));

                        e.ConfigureConsumer<PaymentNotificationConsumer>(context);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    var options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
                                  ?? new RabbitMqOptions();

                    cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                    {
                        h.Username(options.Username);
                        h.Password(options.Password);
                    });

                    cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)));

                    cfg.ReceiveEndpoint("notification-audit-service.payment-succeeded", e =>
                    {
                        e.PrefetchCount = 32;
                        e.ConcurrentMessageLimit = 16;
                        e.UseMessageRetry(r => r.Intervals(
                            TimeSpan.FromSeconds(2),
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromSeconds(10)));

                        e.Bind("payment.payment-succeeded", b =>
                        {
                            b.ExchangeType = "topic";
                        });
                        e.Bind<PaymentSucceededV1>();

                        e.ConfigureConsumer<PaymentNotificationConsumer>(context);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }
}
