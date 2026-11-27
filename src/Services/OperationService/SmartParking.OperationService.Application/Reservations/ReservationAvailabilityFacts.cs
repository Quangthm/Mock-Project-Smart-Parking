using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationAvailabilityFacts(IReadOnlyList<ReservationRow> Reservations,
    IReadOnlyList<ParkingSessionRow> Sessions, IReadOnlyList<SlotAllocationRow> Allocations,
    int? MaxReservationQuota)
{
    internal AvailabilityDecision EvaluateReservation(ReservationRow proposed, DateTimeOffset evaluatedAt,
        TimeSpan projection, CancellationToken ct)
    {
        var (commitments, visits, unresolved, proposal) = BuildInputs(proposed.VehicleType, proposed);
        return ReservationAvailability.Evaluate(proposal!, commitments, visits,
            MaxReservationQuota, evaluatedAt, projection, unresolved, ct);
    }

    internal AvailabilityDecision EvaluatePhysical(Guid slotId, ParkingSessionRow arrivingSession, DateTimeOffset evaluatedAt)
    {
        var (commitments, visits, unresolved, _) = BuildInputs(null, null);
        // Reserved scans already established their own assignment. Check competing commitments and visits.
        return ReservationAvailability.EvaluatePhysicalOccupancy(slotId, evaluatedAt,
            commitments.Where(commitment => commitment.Id != arrivingSession.ReservationId).ToArray(),
            visits.Where(visit => visit.Id != arrivingSession.Id).ToArray(), unresolved);
    }

    public int? CountAvailableCars(IReadOnlyCollection<Guid> candidateSlots, ReservationInterval? effectiveInterval,
        DateTimeOffset evaluatedAt, TimeSpan projection, CancellationToken ct)
    {
        var (commitments, visits, unresolved, _) = BuildInputs("CAR", null);
        return ReservationAvailability.CountAvailable(candidateSlots, effectiveInterval, evaluatedAt, projection,
            commitments, visits, unresolved, MaxReservationQuota, ct);
    }

    private (CapacityDemand[] Commitments, CapacityVisit[] Visits, CapacityAllocation[] Unresolved, CapacityDemand? Proposal)
        BuildInputs(string? quotaCategory, ReservationRow? proposed)
    {
        var byReservation = Allocations.ToLookup(a => a.ReservationId);
        var bySession = Allocations.ToLookup(a => a.ParkingSessionId);
        var sessions = Sessions.ToDictionary(s => s.Id);
        CapacityAllocation Interval(SlotAllocationRow allocation)
        {
            var observed = allocation.ParkingSessionId is Guid id && sessions.TryGetValue(id, out var session) &&
                session.ReservationId == allocation.ReservationId && session.CurrentSlotId == allocation.SlotId
                    ? session.LocationScannedAt : null;
            return new(allocation.SlotId, allocation.AllocatedStartTime, allocation.AllocatedEndTime,
                allocation.AllocationStatus, allocation.ReservationId, allocation.ParkingSessionId, observed);
        }

        CapacityDemand Demand(ReservationRow row) => new(row.Id, row.ActiveClaimSlotId,
            row.ExpectedStartTime, row.ClaimEndTime ?? row.ExpectedEndTime,
            row.VehicleType == quotaCategory, row.VehicleType, byReservation[row.Id].Select(Interval).ToArray(),
            row.TargetSlotId);
        var commitments = Reservations.Select(Demand).ToArray();
        var visits = Sessions.Select(s => new CapacityVisit(s.Id, s.ReservationId, s.CurrentSlotId, s.EntryTime,
            s.VehicleType, bySession[s.Id].Select(Interval).ToArray())).ToArray();
        var reservationIds = Reservations.Select(r => r.Id).ToHashSet();
        var unresolved = Allocations.Where(a =>
            !(a.ReservationId is Guid owner && reservationIds.Contains(owner)) &&
            !(a.ParkingSessionId is Guid id && sessions.ContainsKey(id))).Select(Interval).ToArray();
        return (commitments, visits, unresolved, proposed is null ? null : Demand(proposed));
    }
}
