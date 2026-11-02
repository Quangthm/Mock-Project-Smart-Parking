namespace SmartParking.OperationService.Application.Common.Interfaces.Services;

public interface IOutboxEventPublisher
{
    string Name { get; }

    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}

public sealed record OutboxMessage(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    long SequenceNumber,
    Guid SiteId,
    string Payload);
