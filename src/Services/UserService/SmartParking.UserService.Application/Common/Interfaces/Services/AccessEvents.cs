namespace UserService.Application.Common.Interfaces.Services;

// Versioned public integration contract. Never include contacts, passwords, OTPs or capabilities.
public sealed record OperatorAccessChanged(Guid EventId, int SchemaVersion, Guid AggregateId, long AggregateVersion,
    DateTimeOffset OccurredAt, Guid OperationId, Guid ActorId, Guid[] TenantIds, string Status);
public sealed record AccessEventLease(OperatorAccessChanged Message, Guid LeaseId);
public interface IAccessEventStore
{
    void Stage(OperatorAccessChanged message);
    Task<AccessEventLease?> ClaimAsync(CancellationToken ct);
    Task CompleteAsync(AccessEventLease lease, CancellationToken ct);
    Task<bool> ApplyProjectionAsync(OperatorAccessChanged message, CancellationToken ct);
}
public interface IAccessEventTransport
{
    Task PublishAsync(OperatorAccessChanged message, CancellationToken ct);
}
public interface IAccessChangeNotifier
{
    Task NotifyAsync(OperatorAccessChanged message, CancellationToken ct);
}

