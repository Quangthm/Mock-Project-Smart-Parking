using MassTransit;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;

namespace SmartParking.PaymentService.Persistence.Outbox;

public class OutboxEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OutboxEventPublisher> _logger;

    public OutboxEventPublisher(IPublishEndpoint publishEndpoint, ILogger<OutboxEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
    {
        await _publishEndpoint.Publish(@event, cancellationToken);
        _logger.LogInformation("Enqueued integration event {EventType} into MassTransit transactional outbox.", typeof(TEvent).Name);
    }
}
