namespace SmartParking.OperationService.Application.Common.Interfaces.Services;

public sealed record OutboxMessage(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    long SequenceNumber,
    Guid SiteId,
    string Payload);