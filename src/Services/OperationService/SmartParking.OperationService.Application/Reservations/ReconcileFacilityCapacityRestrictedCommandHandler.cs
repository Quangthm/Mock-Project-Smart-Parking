using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

// Facility withdrew capacity for a site, an area or a single bay. The booking is never destroyed
// blindly: an active booking is first moved to a verified free bay, and only when no safe
// replacement exists is it cancelled with FACILITY_FAILURE evidence and refunded. A parked vehicle
// (PARKING) is never touched. Payment calls happen after the database transaction is closed.
public sealed class ReconcileFacilityCapacityRestrictedCommandHandler(
    IEmergencyClosureReconciliationStore store,
    IReservationBookingRepository bookings,
    IEmergencyReallocationSlotProvider slots,
    IReservationAvailabilityRepository availability,
    IOperationsConcurrency guards,
    IReservationCommercialOwner commercial,
    IUnitOfWork work,
    TimeProvider clock) : IRequestHandler<ReconcileFacilityCapacityRestrictedCommand>
{
    public async Task Handle(ReconcileFacilityCapacityRestrictedCommand command, CancellationToken ct)
    {
        // The mirror row is what admission reads, so nothing else happens for a redelivery.
        if (await store.HasProcessedEventAsync(command.EventId, ct)) return;

        var receivedAt = clock.GetUtcNow();
        await store.UpsertEmergencyClosureAsync(new StructureEmergencyClosureRow
        {
            RestrictionId = command.RestrictionId,
            TenantId = command.TenantId,
            SiteId = command.SiteId,
            Mode = command.Mode,
            Scope = command.Scope,
            UnitId = command.UnitId,
            AffectedSlotIds = JsonSerializer.Serialize(command.AffectedSlotIds),
            Reason = command.Reason,
            EffectiveFrom = command.EffectiveFrom,
            EffectiveUntil = command.EffectiveUntil,
            ReceivedAt = receivedAt
        }, ct);

        await CapacityClosureReconciler.ReconcileAsync(
            new CapacityWithdrawalSpec(
                command.RestrictionId,
                command.TenantId,
                command.SiteId,
                command.Scope,
                command.UnitId,
                [],
                command.AffectedSlotIds,
                command.EffectiveFrom,
                command.EffectiveUntil),
            store, bookings, slots, availability, guards, commercial, work, clock, ct);

        // Written last: if a refund call above throws, the transport retry repeats this delivery
        // and every step is idempotent.
        await store.RecordProcessedEventAsync(new FacilityEventReceiptRow
        {
            EventId = command.EventId,
            EventType = "FacilityCapacityRestrictedV1",
            SiteId = command.SiteId,
            ProcessedAt = clock.GetUtcNow()
        }, ct);
    }
}
