using MediatR;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ReleaseFacilityCapacityRestrictionCommandHandler(
    IEmergencyClosureReconciliationStore store,
    TimeProvider clock) : IRequestHandler<ReleaseFacilityCapacityRestrictionCommand>
{
    public async Task Handle(ReleaseFacilityCapacityRestrictionCommand command, CancellationToken ct)
    {
        // Duplicate redelivery must not reopen anything twice, and no receipt is written until the
        // release is actually mirrored, so a failed attempt is retried by the transport.
        if (await store.HasProcessedEventAsync(command.EventId, ct)) return;

        await store.MarkClosureReleasedAsync(
            command.RestrictionId, command.TenantId, command.SiteId, clock.GetUtcNow(), ct);

        await store.RecordProcessedEventAsync(new FacilityEventReceiptRow
        {
            EventId = command.EventId,
            EventType = "FacilityCapacityRestrictionReleasedV1",
            SiteId = command.SiteId,
            ProcessedAt = clock.GetUtcNow()
        }, ct);
    }
}