using System.Text.Json;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

// Shared reconciliation workflow for Capacity withdrawal events (emergency restrictions and
// scheduled maintenance). Reallocates active non-SITE bookings when a safe bay exists, or cancels
// with FACILITY_FAILURE evidence and invokes PaymentService outside any open DB transaction.
// Parked vehicles (PARKING) are never touched.
internal static class CapacityClosureReconciler
{
    private const string ReconciledCode = "FACILITY_FAILURE_RECONCILED";

    public static async Task ReconcileAsync(
        CapacityWithdrawalSpec spec,
        IEmergencyClosureReconciliationStore store,
        IReservationBookingRepository bookings,
        IEmergencyReallocationSlotProvider slots,
        IReservationAvailabilityRepository availability,
        IOperationsConcurrency guards,
        IReservationCommercialOwner commercial,
        IUnitOfWork work,
        TimeProvider clock,
        CancellationToken ct)
    {
        var candidates = await store.FindEmergencyCandidatesAsync(
            spec.TenantId, spec.SiteId, spec.EffectiveFrom, spec.EffectiveUntil, ct);

        foreach (var candidate in candidates)
        {
            if (candidate.Status == "CANCELLED")
            {
                if (candidate.ConfirmationState == "RECONCILIATION_REQUIRED" &&
                    candidate.ConfirmationCode == $"FACILITY_FAILURE:{spec.SourceId:D}")
                    await CompletePaymentReconciliationAsync(
                        candidate, spec.SourceId, bookings, availability, guards, commercial, work, ct);
                continue;
            }

            var allocationSlots = (await store.FindActiveAllocationsAsync(
                spec.TenantId, spec.SiteId, candidate.Id, ct)).Select(a => a.SlotId).ToArray();
            if (!Covers(spec, candidate, allocationSlots)) continue;

            var replacement = spec.Scope == "SITE"
                ? null
                : await FindReplacementAsync(spec, candidate, slots, store, ct);

            var cancelled = await ApplyAsync(
                spec, candidate, replacement, store, bookings, availability, guards, work, clock, ct);
            if (cancelled is not null)
                await CompletePaymentReconciliationAsync(
                    cancelled, spec.SourceId, bookings, availability, guards, commercial, work, ct);
        }
    }

    private static bool Covers(
        CapacityWithdrawalSpec spec, ReservationRow reservation, IReadOnlyList<Guid> allocationSlots)
    {
        var reservationEnd = reservation.ClaimEndTime ?? reservation.ExpectedEndTime;
        if (spec.EffectiveFrom >= reservationEnd) return false;
        if (spec.EffectiveUntil is { } until && until <= reservation.ExpectedStartTime) return false;

        if (spec.Scope == "SITE") return true;
        if (reservation.ActiveClaimSlotId is { } claimed && spec.AffectedSlotIds.Contains(claimed)) return true;
        if (reservation.TargetSlotId is { } target && spec.AffectedSlotIds.Contains(target)) return true;
        if (allocationSlots.Any(slot => spec.AffectedSlotIds.Contains(slot))) return true;
        if (spec.Scope != "UNIT") return false;

        return (spec.UnitId is { } unit && reservation.TargetSpatialUnitId == unit) ||
               (reservation.TargetSpatialUnitId is { } targetUnit && spec.ScopedUnitIds.Contains(targetUnit));
    }

    private static async Task<Guid?> FindReplacementAsync(
        CapacityWithdrawalSpec spec,
        ReservationRow reservation,
        IEmergencyReallocationSlotProvider slots,
        IEmergencyClosureReconciliationStore store,
        CancellationToken ct)
    {
        var catalogue = await slots.ListSiteSlotCandidatesAsync(spec.SiteId, ct);
        if (catalogue.Count == 0) return null;

        var reservationEnd = reservation.ClaimEndTime ?? reservation.ExpectedEndTime;
        var maintenance = await store.FindActiveMaintenanceClosuresAsync(spec.TenantId, spec.SiteId, ct);
        var emergencies = await store.FindActiveEmergencyClosuresAsync(spec.TenantId, spec.SiteId, ct);
        var others = await store.FindSiteActiveReservationsForReallocationAsync(
            spec.TenantId, spec.SiteId, reservation.ExpectedStartTime, reservationEnd, ct);
        var sessions = await store.FindSiteOpenSessionsAsync(spec.TenantId, spec.SiteId, ct);

        var viable = catalogue.Where(candidate =>
        {
            if (!candidate.Eligible || !candidate.Active || !candidate.Reachable) return false;
            if (!candidate.Supports(reservation.VehicleType)) return false;
            if (spec.AffectedSlotIds.Contains(candidate.SlotId)) return false;
            if (maintenance.Any(window => window.CancelledAt is null &&
                    window.WindowStart < reservationEnd &&
                    (window.WindowEnd is null || window.WindowEnd > reservation.ExpectedStartTime) &&
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

        return viable
            .OrderBy(candidate => candidate.SpatialUnitId == preferredUnit && !candidate.Backup ? 0
                : candidate.Backup ? 1 : 2)
            .ThenBy(candidate => candidate.SlotId)
            .First().SlotId;
    }

    private static async Task<ReservationRow?> ApplyAsync(
        CapacityWithdrawalSpec spec,
        ReservationRow observed,
        Guid? replacement,
        IEmergencyClosureReconciliationStore store,
        IReservationBookingRepository bookings,
        IReservationAvailabilityRepository availability,
        IOperationsConcurrency guards,
        IUnitOfWork work,
        TimeProvider clock,
        CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        await availability.LockAccountAsync(observed.AccountId, ct);
        if (!string.IsNullOrWhiteSpace(observed.NormalizedPlate))
            await guards.LockVehicleAsync(observed.NormalizedPlate, ct);
        await guards.LockSiteAsync(spec.SiteId, ct);

        var booking = await bookings.FindForUpdateAsync(spec.TenantId, spec.SiteId, observed.Id, ct);
        if (booking is null || booking.Status is not ("PENDING_PAYMENT" or "CONFIRMED" or "ALLOCATED"))
        {
            await transaction.CommitAsync(ct);
            return null;
        }

        var target = replacement;
        if (target is { } candidateSlot &&
            !await IsReplacementFreeUnderLockAsync(spec.TenantId, spec.SiteId, booking, candidateSlot, store, ct))
            target = null;

        if (target is { } targetSlot)
        {
            booking.ReallocateSlot(targetSlot);
            var allocation = await work.SlotAllocations.FindReservedAsync(
                spec.TenantId, spec.SiteId, booking.Id, ct);
            allocation?.MoveTo(targetSlot);
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return null;
        }

        var previousStatus = booking.Status;
        var now = clock.GetUtcNow();
        var requiresPayment = booking.PaymentOrderId is not null && (booking.DepositAmount ?? 0m) > 0m;

        booking.CancelForFacilityFailure(now, requiresPayment, spec.SourceId);

        var allocations = await store.FindActiveAllocationsAsync(spec.TenantId, spec.SiteId, booking.Id, ct);
        foreach (var allocation in allocations) allocation.Release(now);

        await store.DecrementCapacityPoolAsync(spec.TenantId, spec.SiteId, booking.VehicleType,
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

    private static async Task CompletePaymentReconciliationAsync(
        ReservationRow observed,
        Guid sourceId,
        IReservationBookingRepository bookings,
        IReservationAvailabilityRepository availability,
        IOperationsConcurrency guards,
        IReservationCommercialOwner commercial,
        IUnitOfWork work,
        CancellationToken ct)
    {
        if (observed.Status != "CANCELLED" ||
            observed.ConfirmationState != "RECONCILIATION_REQUIRED" ||
            observed.PaymentOrderId is not { } orderId) return;

        var collection = await commercial.CollectionsAsync(observed.Id, orderId, ct);
        var reason = $"FACILITY_FAILURE:{sourceId:D}";
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

    private static async Task<bool> IsReplacementFreeUnderLockAsync(
        Guid tenantId,
        Guid siteId,
        ReservationRow booking,
        Guid slotId,
        IEmergencyClosureReconciliationStore store,
        CancellationToken ct)
    {
        var reservationEnd = booking.ClaimEndTime ?? booking.ExpectedEndTime;
        var occupied = await store.FindSiteActiveReservationsForReallocationAsync(
            tenantId, siteId, booking.ExpectedStartTime, reservationEnd, ct);
        if (occupied.Any(other => other.Id != booking.Id && SlotOf(other) == slotId &&
                other.ExpectedStartTime < reservationEnd &&
                (other.ClaimEndTime ?? other.ExpectedEndTime) > booking.ExpectedStartTime))
            return false;

        var sessions = await store.FindSiteOpenSessionsAsync(tenantId, siteId, ct);
        return !sessions.Any(session => session.CurrentSlotId == slotId);
    }

    private static bool CoveredByMaintenance(StructureMaintenanceClosureRow window, FacilitySlotCandidate candidate)
    {
        if (window.SlotId is { } windowSlot && windowSlot == candidate.SlotId) return true;
        if (ParseIds(window.ScopedSlotIds).Contains(candidate.SlotId)) return true;
        if (window.UnitId is null) return window.SlotId is null;
        return candidate.SpatialUnitId == window.UnitId ||
               (candidate.AncestorUnitIds?.Contains(window.UnitId.Value) ?? false) ||
               (candidate.SpatialUnitId is { } candidateUnit && ParseIds(window.ScopedUnitIds).Contains(candidateUnit));
    }

    private static bool CoveredByEmergency(StructureEmergencyClosureRow closure, FacilitySlotCandidate candidate)
    {
        if (closure.Scope == "SITE") return true;
        if (ParseIds(closure.AffectedSlotIds).Contains(candidate.SlotId)) return true;
        return closure.Scope == "UNIT" && closure.UnitId is { } unit &&
               (candidate.SpatialUnitId == unit || (candidate.AncestorUnitIds?.Contains(unit) ?? false));
    }

    private static HashSet<Guid> ParseIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<HashSet<Guid>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
