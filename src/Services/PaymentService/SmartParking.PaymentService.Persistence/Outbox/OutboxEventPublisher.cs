using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Persistence.Entities;

namespace SmartParking.PaymentService.Persistence.Outbox;

public class OutboxEventPublisher : IEventPublisher
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<OutboxEventPublisher> _logger;

    public OutboxEventPublisher(PaymentDbContext dbContext, ILogger<OutboxEventPublisher> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
    {
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = typeof(TEvent).Name,
            Payload = JsonSerializer.Serialize(@event),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        _logger.LogInformation("Staged outbox integration event {EventType} with ID {EventId}.", outboxMessage.EventType, outboxMessage.Id);
    }
}
