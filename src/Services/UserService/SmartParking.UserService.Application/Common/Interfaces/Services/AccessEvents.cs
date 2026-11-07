using UserService.Application.Common.Interfaces.MessageBroker;

namespace UserService.Application.Common.Interfaces.Services;

// Versioned public integration contract. Never include contacts, passwords, OTPs or capabilities.
public sealed record OperatorAccessChanged(Guid EventId, int SchemaVersion, Guid AggregateId, long AggregateVersion,
    DateTimeOffset OccurredAt, Guid OperationId, Guid ActorId, Guid[] TenantIds, string Status) : IIntegrationEventMessage;

public interface IAccessChangeNotifier
{
    Task NotifyAsync(OperatorAccessChanged message, CancellationToken ct);
}
