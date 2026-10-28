using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.NotificationAuditService.Infrastructure.Consumers;
using SmartParking.NotificationAuditService.Infrastructure.Options;

namespace SmartParking.NotificationAuditService.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        var useInMemory = configuration.GetValue<bool>("RabbitMQ:UseInMemory");

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddConsumer<AuditProjectionConsumer>();

            if (useInMemory)
            {
                x.UsingInMemory((context, cfg) =>
                {
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

                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }
}
