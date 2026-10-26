using MassTransit;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Profiles.Events;

namespace UserService.Infrastructure.Messaging;

/// <summary>
/// Resolves the scoped publish endpoint, which the EF Bus Outbox replaces with durable capture.
/// Never inject IBus here: doing so would bypass the request's AppDbContext and transaction.
/// </summary>
public sealed class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, IIntegrationEventMessage =>
        publishEndpoint.Publish(message, context =>
        {
            if (message is UserProfileNameChanged profile)
            {
                context.MessageId = profile.EventId;
                context.Headers.Set("correlation-id", profile.CorrelationId);
            }
        }, cancellationToken);

    public async Task PublishManyAsync<TMessage>(IEnumerable<TMessage> messages, CancellationToken cancellationToken)
        where TMessage : class, IIntegrationEventMessage
    {
        // The scoped DbContext is not thread-safe.
        foreach (var message in messages)
            await PublishAsync(message, cancellationToken);
    }
}
