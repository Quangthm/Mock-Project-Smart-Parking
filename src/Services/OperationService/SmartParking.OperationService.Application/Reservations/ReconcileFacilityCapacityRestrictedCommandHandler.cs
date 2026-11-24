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
    private const string ReconciledCode = "FACILITY_FAILURE_RECONCILED";

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

        var candidates = await store.FindEmergencyCandidatesAsync(command.TenantId, command.SiteId,
            command.EffectiveFrom, command.EffectiveUntil, ct);

        foreach (var candidate in candidates)
        {
            // A booking already cancelled by an earlier delivery may still owe its refund; retrying
            // that is the only reason a cancelled row is listed here, and only the restriction that
            // wrote the pending refund code may complete it.
            if (candidate.Status == "CANCELLED")
            {
                if (candidate.ConfirmationState == "RECONCILIATION_REQUIRED" &&
                    candidate.ConfirmationCode == $"FACILITY_FAILURE:{command.RestrictionId:D}")
                    await CompletePaymentReconciliationAsync(candidate, command.RestrictionId, ct);
                continue;
            }

            var allocationSlots = await ReservedAllocationSlotsAsync(command, candidate, ct);
            if (!Covers(command, candidate, allocationSlots)) continue;

            var replacement = command.Scope == "SITE"
                ? null
                : await FindReplacementAsync(command, candidate, ct);

            // ApplyAsync returns the freshly reloaded booking only when it was cancelled, so the
            // refund step sees the committed state instead of the stale untracked snapshot.
            var cancelled = await ApplyAsync(command, candidate, replacement, ct);
            if (cancelled is not null)
                await CompletePaymentReconciliationAsync(cancelled, command.RestrictionId, ct);
        }

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

    // Overlap (half-open) plus scope: SITE covers everything, otherwise the affected bay list, the
    // reserved area or a reserved allocation decides.
    private static bool Covers(ReconcileFacilityCapacityRestrictedCommand command, ReservationRow reservation,
        IReadOnlyList<Guid> allocationSlots)
    {
        var reservationEnd = reservation.ClaimEndTime ?? reservation.ExpectedEndTime;
        if (command.EffectiveFrom >= reservationEnd) return false;
        if (command.EffectiveUntil is { } until && until <= reservation.ExpectedStartTime) return false;

        if (command.Scope == "SITE") return true;
        if (reservation.ActiveClaimSlotId is { } claimed && command.AffectedSlotIds.Contains(claimed)) return true;
        if (reservation.TargetSlotId is { } target && command.AffectedSlotIds.Contains(target)) return true;
        // An ALLOCATED booking may carry no target slot at all; its reserved allocation is then the
        // only record of which physical bay was withdrawn.
        if (allocationSlots.Any(slot => command.AffectedSlotIds.Contains(slot))) return true;
        return command.Scope == "UNIT" && command.UnitId is { } unit &&
               reservation.TargetSpatialUnitId == unit;
    }

    // Step A: everything that needs a facility read happens before any database transaction opens.
    private async Task<Guid?> FindReplacementAsync(
        ReconcileFacilityCapacityRestrictedCommand command,
        ReservationRow reservation,
        CancellationToken ct)
    {
        var catalogue = await slots.ListSiteSlotCandidatesAsync(command.SiteId, ct);
        if (catalogue.Count == 0) return null;

        var reservationEnd = reservation.ClaimEndTime ?? reservation.ExpectedEndTime;
        var maintenance = await store.FindActiveMaintenanceClosuresAsync(command.TenantId, command.SiteId, ct);
        var emergencies = await store.FindActiveEmergencyClosuresAsync(command.TenantId, command.SiteId, ct);
        var others = await store.FindSiteActiveReservationsForReallocationAsync(command.TenantId,
            command.SiteId, reservation.ExpectedStartTime, reservationEnd, ct);
        var sessions = await store.FindSiteOpenSessionsAsync(command.TenantId, command.SiteId, ct);

        var viable = catalogue.Where(candidate =>
        {
            if (!candidate.Eligible || !candidate.Active || !candidate.Reachable) return false;
            if (!candidate.Supports(reservation.VehicleType)) return false;
            if (command.AffectedSlotIds.Contains(candidate.SlotId)) return false;
            if (maintenance.Any(window => window.CancelledAt is null &&
                    window.WindowStart < reservationEnd && window.WindowEnd > reservation.ExpectedStartTime &&
                    CoveredByMaintenance(window, candidate))) return false;
            if (emergencies.Any(closure => closure.ReleasedAt is null &&
                    closure.EffectiveFrom < reservationEnd &&
                    (closure.EffectiveUntil is null || closure.EffectiveUntil > reservation.ExpectedStartTime) &&
                    CoveredByEmergency(closure, candidate))) return false;
            if (others.Any(other => other.Id != reservation.Id && SlotOf(other) == candidate.SlotId &&
                    other.ExpectedStartTime < reservationEnd &&
                    (other.ClaimEndTime ?? other.ExpectedEndTime) > reservation.ExpectedStartTime)) return false;
            return !sessions.Any(session => session.CurrentSlotId == candidate.SlotId);
        }).ToArray();

        if (viable.Length == 0) return null;

        var preferredUnit = reservation.ActiveClaimSlotId is { } claimedSlot
            ? catalogue.FirstOrDefault(candidate => candidate.SlotId == claimedSlot)?.SpatialUnitId
            : null;

        // Stay in the same area when possible, then use a designated backup bay, then any fit.
        return viable
            .OrderBy(candidate => candidate.SpatialUnitId == preferredUnit && !candidate.Backup ? 0
                : candidate.Backup ? 1 : 2)
            .ThenBy(candidate => candidate.SlotId)
            .First().SlotId;
    }

    // Step B: short transaction with the canonical guard order, re-read, then mutate.
    private async Task<ReservationRow?> ApplyAsync(
        ReconcileFacilityCapacityRestrictedCommand command,
        ReservationRow observed,
        Guid? replacement,
        CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        await availability.LockAccountAsync(observed.AccountId, ct);
        if (!string.IsNullOrWhiteSpace(observed.NormalizedPlate))
            await guards.LockVehicleAsync(observed.NormalizedPlate, ct);
        await guards.LockSiteAsync(command.SiteId, ct);

        var booking = await bookings.FindForUpdateAsync(command.TenantId, command.SiteId, observed.Id, ct);
        if (booking is null || booking.Status is not ("PENDING_PAYMENT" or "CONFIRMED" or "ALLOCATED"))
        {
            await transaction.CommitAsync(ct);
            return null;
        }

        // The replacement bay was chosen outside the transaction; re-verify it under the site guard
        // because another writer may have taken it in the meantime. A clash cancels instead.
        var target = replacement;
        if (target is { } candidateSlot &&
            !await IsReplacementFreeUnderLockAsync(command, booking, candidateSlot, ct))
            target = null;

        if (target is { } targetSlot)
        {
            booking.ReallocateSlot(targetSlot);
            var allocation = await work.SlotAllocations.FindReservedAsync(
                command.TenantId, command.SiteId, booking.Id, ct);
            allocation?.MoveTo(targetSlot);
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return null;
        }

        var previousStatus = booking.Status;
        var now = clock.GetUtcNow();
        var requiresPayment = booking.PaymentOrderId is not null && (booking.DepositAmount ?? 0m) > 0m;

        booking.CancelForFacilityFailure(now, requiresPayment, command.RestrictionId);

        var allocations = await store.FindActiveAllocationsAsync(command.TenantId, command.SiteId, booking.Id, ct);
        foreach (var allocation in allocations) allocation.Release(now);

        await store.DecrementCapacityPoolAsync(command.TenantId, command.SiteId, booking.VehicleType,
            previousStatus, booking.TargetSpatialUnitId, ct);

        if (!await store.HasCancellationEvidenceAsync(booking.Id, ct))
        {
            store.AddCancellationEvidence(ReservationCancellationEvidence.Record(
                Guid.NewGuid(),
                booking.Id,
                booking.AccountId,
                now,
                "FACILITY_FAILURE",
                null,
                string.IsNullOrWhiteSpace(booking.AcceptedOperationalPolicySnapshot)
                    ? "{}"
                    : booking.AcceptedOperationalPolicySnapshot));
        }

        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return booking;
    }

    // Step C: never inside a database transaction. PaymentService deduplicates per collection and
    // reason, so a retry after a partial batch is safe.
    private async Task CompletePaymentReconciliationAsync(
        ReservationRow observed, Guid restrictionId, CancellationToken ct)
    {
        if (observed.Status != "CANCELLED" ||
            observed.ConfirmationState != "RECONCILIATION_REQUIRED" ||
            observed.PaymentOrderId is not { } orderId) return;

        var collection = await commercial.CollectionsAsync(observed.Id, orderId, ct);
        var reason = $"FACILITY_FAILURE:{restrictionId:D}";
        foreach (var item in collection.Collections)
            await commercial.ReconcileAsync(observed.Id, item, reason, ct);

        await using var transaction = await work.BeginTransactionAsync(ct);
        await availability.LockAccountAsync(observed.AccountId, ct);
        if (!string.IsNullOrWhiteSpace(observed.NormalizedPlate))
            await guards.LockVehicleAsync(observed.NormalizedPlate, ct);
        await guards.LockSiteAsync(observed.SiteId, ct);

        var booking = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, observed.Id, ct);
        if (booking is null || booking.Status != "CANCELLED")
        {
            await transaction.CommitAsync(ct);
            return;
        }

        booking.MarkReconciliationCompleted(ReconciledCode);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static Guid? SlotOf(ReservationRow reservation)
        => reservation.ActiveClaimSlotId ?? reservation.TargetSlotId;

    private async Task<IReadOnlyList<Guid>> ReservedAllocationSlotsAsync(
        ReconcileFacilityCapacityRestrictedCommand command, ReservationRow reservation, CancellationToken ct)
    {
        var allocations = await store.FindActiveAllocationsAsync(command.TenantId, command.SiteId,
            reservation.Id, ct);
        return allocations.Select(allocation => allocation.SlotId).ToArray();
    }

    // Re-verified under the site guard. The scan that picked the bay ran before the transaction, so
    // the answer is not trustworthy until the guard is held and the current commitments are re-read.
    private async Task<bool> IsReplacementFreeUnderLockAsync(
        ReconcileFacilityCapacityRestrictedCommand command, ReservationRow booking, Guid slotId,
        CancellationToken ct)
    {
        var reservationEnd = booking.ClaimEndTime ?? booking.ExpectedEndTime;
        var occupied = await store.FindSiteActiveReservationsForReallocationAsync(command.TenantId,
            command.SiteId, booking.ExpectedStartTime, reservationEnd, ct);
        if (occupied.Any(other => other.Id != booking.Id && SlotOf(other) == slotId &&
                other.ExpectedStartTime < reservationEnd &&
                (other.ClaimEndTime ?? other.ExpectedEndTime) > booking.ExpectedStartTime))
            return false;

        var sessions = await store.FindSiteOpenSessionsAsync(command.TenantId, command.SiteId, ct);
        return !sessions.Any(session => session.CurrentSlotId == slotId);
    }

    private static bool CoveredByMaintenance(StructureMaintenanceClosureRow window, FacilitySlotCandidate candidate)
    {
        if (window.SlotId is { } windowSlot) return windowSlot == candidate.SlotId;
        if (window.UnitId is not { } unit) return true;
        return candidate.SpatialUnitId == unit || (candidate.AncestorUnitIds?.Contains(unit) ?? false);
    }

    private static bool CoveredByEmergency(StructureEmergencyClosureRow closure, FacilitySlotCandidate candidate)
    {
        if (closure.Scope == "SITE") return true;
        if (AffectedSlots(closure).Contains(candidate.SlotId)) return true;
        return closure.Scope == "UNIT" && closure.UnitId is { } unit &&
               (candidate.SpatialUnitId == unit || (candidate.AncestorUnitIds?.Contains(unit) ?? false));
    }

    private static IReadOnlyList<Guid> AffectedSlots(StructureEmergencyClosureRow closure)
    {
        if (string.IsNullOrWhiteSpace(closure.AffectedSlotIds)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(closure.AffectedSlotIds) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}