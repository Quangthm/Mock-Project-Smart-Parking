using System;

namespace SmartParking.OperationService.Persistence.Entities;

public sealed class OutboxEventRow
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public long SequenceNumber { get; set; }
    public Guid SiteId { get; set; }
    public string Payload { get; set; } = "{}";
    public DateTimeOffset? PublishedAt { get; set; }
}