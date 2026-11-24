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
        var snapshot = command.Resources;
        if (!snapshot.Complete || snapshot.Booking.TenantId != proposed.TenantId ||
            snapshot.Booking.SiteId != proposed.SiteId || snapshot.Booking.SlotId != proposed.TargetSlotId ||
            !snapshot.Booking.EligibleCarSlot || !snapshot.Booking.ParticipatesInStructureGuard)
            throw new ReservationGateException(409, "SLOT_UNAVAILABLE", "The selected slot is unavailable.");
        using var accepted = command.ReacquisitionPolicy is null ? JsonDocument.Parse(proposed.AcceptedOperationalPolicySnapshot!) : null;
        int Minutes(string key) => command.ReacquisitionPolicy?.Values[key] ??
            accepted!.RootElement.GetProperty("Values").GetProperty(key).GetInt32();
        var projection = TimeSpan.FromMinutes(Minutes("OCCUPANCY_VALIDITY_TIMESPAN"));
        var facts = await repository.ReadAsync(proposed, command.EvaluatedAt, ct);
        var resources = snapshot.Resources.Where(r => r.VehicleType == proposed.VehicleType)
            .Select(r => new CapacityResource(r.SlotId, r.SpatialUnitId, r.AncestorUnitIds ?? [],
                r.Active && r.Reachable, r.FixedBackup, r.CompatibleVehicleTypes)).ToArray();
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
            row.TargetSpatialUnitId, row.ExpectedStartTime, row.ClaimEndTime ?? row.ExpectedEndTime,
            row.VehicleType == proposed.VehicleType, row.VehicleType, byReservation[row.Id].Select(Interval).ToArray(), row.TargetSlotId);

        bool UsesCompatibleResources(string category, Guid? slot, IReadOnlyList<CapacityAllocation>? allocations) =>
            category == proposed.VehicleType || resources.Any(r => slot is null && r.CompatibleCategories?.Contains(category) == true ||
                r.Id == slot || allocations?.Any(a => a.Slot == r.Id) == true);

        var claims = facts.Reservations.Select(Demand)
            .Where(d => UsesCompatibleResources(d.Category, d.Slot ?? d.TargetSlot, d.Allocations)).ToArray();
        var visits = facts.Sessions.Select(s => new CapacityVisit(s.Id, s.ReservationId, s.CurrentSlotId, s.EntryTime,
                s.VehicleType, bySession[s.Id].Select(Interval).ToArray()))
            .Where(v => UsesCompatibleResources(v.Category, v.Slot, v.Allocations)).ToArray();
        var reservationIds = facts.Reservations.Select(r => r.Id).ToHashSet();
        var unresolved = facts.Allocations.Where(a =>
            !(a.ReservationId is Guid owner && reservationIds.Contains(owner)) &&
            !(a.ParkingSessionId is Guid id && sessions.ContainsKey(id))).Select(Interval).ToArray();

        // The fixed-slot workflow has no Operations-local source for ordinary supply by zone/floor.
        // This matters only while an overlapping compatible commitment still needs an unassigned unit.
        var result = ReservationAvailability.Evaluate(Demand(proposed), claims, visits, resources,
            snapshot.DynamicBackups.Where(b => b.VehicleType == proposed.VehicleType)
                .Select(b => new CapacityBackup(b.SpatialUnitId, b.Count)).ToArray(),
            facts.Quotas, command.EvaluatedAt, projection, hasScopedCapacityEvidence: false, unresolved, ct);
        if (!result.Allowed)
            throw new ReservationGateException(result.Indeterminate ? 503 : 409,
                result.Rejection!, "The requested capacity is unavailable or requires reconciliation.");
    }
}
