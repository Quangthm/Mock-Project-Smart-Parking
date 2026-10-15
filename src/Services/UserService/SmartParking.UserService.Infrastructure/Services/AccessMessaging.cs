using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Infrastructure.Services;

public static class AccessMessaging
{
    public static IServiceCollection AddAccessMessaging(this IServiceCollection services, IConfiguration config)
    {
        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<AccessProjectionConsumer>();
            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(config["Messaging:Host"] ?? "localhost", config.GetValue<ushort>("Messaging:Port", 5672),
                    config["Messaging:VirtualHost"] ?? "/", host =>
                {
                    host.Username(config["Messaging:Username"] ?? throw new InvalidOperationException("Configure Messaging:Username."));
                    host.Password(config["Messaging:Password"] ?? throw new InvalidOperationException("Configure Messaging:Password."));
                });
                rabbit.ReceiveEndpoint("smartpark-user-access-projection-v1", endpoint =>
                {
                    endpoint.UseMessageRetry(retry => retry.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                    endpoint.ConfigureConsumer<AccessProjectionConsumer>(context);
                });
            });
        });
        services.AddScoped<IAccessEventTransport, RabbitAccessEventTransport>();
        services.AddHostedService<AccessOutboxWorker>();
        return services;
    }
}
public sealed class RabbitAccessEventTransport(IPublishEndpoint publisher) : IAccessEventTransport
{
    public Task PublishAsync(OperatorAccessChanged message, CancellationToken ct) =>
        publisher.Publish(message, context =>
        {
            context.MessageId = message.EventId;
            context.CorrelationId = message.OperationId;
        }, ct);
}
public sealed class AccessProjectionConsumer(IAccessEventStore store, IAccessChangeNotifier notifier)
    : IConsumer<OperatorAccessChanged>
{
    public async Task Consume(ConsumeContext<OperatorAccessChanged> context)
    {
        await store.ApplyProjectionAsync(context.Message, context.CancellationToken);
        // Repeat invalidation is safe. Retrying after a notification failure must not lose the hint.
        // Clients reload authoritative state; they never apply event payloads as access decisions.
        await notifier.NotifyAsync(context.Message, context.CancellationToken);
    }
}
public sealed class AccessOutboxWorker(IServiceScopeFactory scopes, ILogger<AccessOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IAccessEventStore>();
                var lease = await store.ClaimAsync(stoppingToken);
                if (lease is not null)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(15));
                    await scope.ServiceProvider.GetRequiredService<IAccessEventTransport>().PublishAsync(lease.Message, timeout.Token);
                    await store.CompleteAsync(lease, stoppingToken);
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning("Access outbox delivery will retry; failure type {FailureType}", exception.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}

