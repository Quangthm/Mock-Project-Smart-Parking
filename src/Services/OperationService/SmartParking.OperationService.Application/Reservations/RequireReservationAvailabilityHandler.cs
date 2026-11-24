using System.Text.Json;
using SmartParking.OperationService.Domain.Entities;
using MediatR;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class RequireReservationAvailabilityHandler(IReservationAvailabilityRepository repository)
    : IRequestHandler<RequireReservationAvailabilityCommand>
{
    public async Task Handle(RequireReservationAvailabilityCommand command, CancellationToken ct)
    {
        var proposed = command.Proposed;
        var snapshot = command.FacilityEvidence;
        if (snapshot.Booking.TenantId != proposed.TenantId ||
            snapshot.Booking.SiteId != proposed.SiteId || snapshot.Booking.SlotId != proposed.TargetSlotId ||
            !snapshot.Booking.EligibleCarSlot || !snapshot.Booking.ParticipatesInStructureGuard)
            throw new ReservationGateException(409, "SLOT_UNAVAILABLE", "The selected slot is unavailable.");
        // The current Facility protocol still delivers a snapshot. Only the slot being evaluated is relevant.
        var selectedSlotId = proposed.ActiveClaimSlotId ?? proposed.TargetSlotId;
        var selected = snapshot.Resources.Where(r => r.SlotId == selectedSlotId).ToArray();
        if (selected.Length > 1)
            throw new ReservationGateException(503, "CAPACITY_CONFIGURATION_REQUIRED", "The selected slot evidence is inconsistent.");
        if (selected.Length == 0 || !selected[0].Active || !selected[0].Reachable || selected[0].FixedBackup ||
            selected[0].VehicleType != proposed.VehicleType ||
            !(selected[0].CompatibleVehicleTypes?.Contains(proposed.VehicleType) ?? proposed.VehicleType == "CAR"))
            throw new ReservationGateException(409, "SLOT_UNAVAILABLE", "The selected slot is unavailable.");

        using var accepted = command.ReacquisitionPolicy is null ? JsonDocument.Parse(proposed.AcceptedOperationalPolicySnapshot!) : null;
        int Minutes(string key) => command.ReacquisitionPolicy?.Values[key] ??
            accepted!.RootElement.GetProperty("Values").GetProperty(key).GetInt32();
        var projection = TimeSpan.FromMinutes(Minutes("OCCUPANCY_VALIDITY_TIMESPAN"));
        var facts = await repository.ReadAsync(proposed, command.EvaluatedAt, ct);
        var byReservation = facts.Allocations.ToLookup(a => a.ReservationId);
        var bySession = facts.Allocations.ToLookup(a => a.ParkingSessionId);
        var sessions = facts.Sessions.ToDictionary(s => s.Id);
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
            row.VehicleType == proposed.VehicleType, row.VehicleType, byReservation[row.Id].Select(Interval).ToArray(), row.TargetSlotId);

        var claims = facts.Reservations.Select(Demand).ToArray();
        var visits = facts.Sessions.Select(s => new CapacityVisit(s.Id, s.ReservationId, s.CurrentSlotId, s.EntryTime,
                s.VehicleType, bySession[s.Id].Select(Interval).ToArray())).ToArray();
        var reservationIds = facts.Reservations.Select(r => r.Id).ToHashSet();
        var unresolved = facts.Allocations.Where(a =>
            !(a.ReservationId is Guid owner && reservationIds.Contains(owner)) &&
            !(a.ParkingSessionId is Guid id && sessions.ContainsKey(id))).Select(Interval).ToArray();

        var result = ReservationAvailability.Evaluate(Demand(proposed), claims, visits,
            facts.Quotas, command.EvaluatedAt, projection, unresolved, ct);
        if (!result.Allowed)
            throw new ReservationGateException(result.Indeterminate ? 503 : 409,
                result.Rejection!, "The requested capacity is unavailable or requires reconciliation.");
    }
}
