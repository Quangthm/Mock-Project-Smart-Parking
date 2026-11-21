using System.Text.Json;
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
        var allocationLead = TimeSpan.FromMinutes(Minutes("ALLOCATION_LEAD_TIME"));
        var facts = await repository.ReadAsync(proposed, command.EvaluatedAt, ct);
        if (facts.Allocations.Any(a => !facts.Reservations.Any(r => r.Id == a.ReservationId) &&
            !facts.Sessions.Any(s => s.Id == a.ParkingSessionId)))
            throw new ReservationGateException(503, "OCCUPANCY_RECONCILIATION_REQUIRED",
                "An allocation has no active reservation or visit to account for its capacity.");
        var resources = snapshot.Resources.Where(r => r.VehicleType == proposed.VehicleType)
            .Select(r => new CapacityResource(r.SlotId, r.SpatialUnitId, r.AncestorUnitIds ?? [],
                r.Active && r.Reachable, r.FixedBackup, r.CompatibleVehicleTypes)).ToArray();
        CapacityDemand Demand(SmartParking.OperationService.Domain.Entities.ReservationRow row)
        {
            var allocated = facts.Allocations.Where(a => a.ReservationId == row.Id)
                .OrderByDescending(a => a.CreatedAt).FirstOrDefault();
            return new(row.Id, row.ActiveClaimSlotId ?? allocated?.SlotId ?? row.TargetSlotId,
                row.TargetSpatialUnitId, row.ExpectedStartTime, row.ClaimEndTime ?? row.ExpectedEndTime,
                row.VehicleType == proposed.VehicleType, row.VehicleType);
        }
        bool UsesCompatibleResources(string category, Guid? slot) => category == proposed.VehicleType ||
            resources.Any(r => r.Id == slot) || slot is null && snapshot.Resources.Any(r =>
                r.VehicleType == proposed.VehicleType && (r.CompatibleVehicleTypes?.Contains(category) ?? false));
        var claims = facts.Reservations.Select(r => (Row: r, Demand: Demand(r)))
            .Where(x => UsesCompatibleResources(x.Row.VehicleType, x.Demand.Slot)).Select(x => x.Demand).ToArray();
        var visits = facts.Sessions.Where(s => UsesCompatibleResources(s.VehicleType,
            s.CurrentSlotId ?? facts.Allocations.FirstOrDefault(a => a.ParkingSessionId == s.Id)?.SlotId))
            .Select(s => new CapacityVisit(s.Id, s.ReservationId,
            s.CurrentSlotId ?? facts.Allocations.FirstOrDefault(a => a.ParkingSessionId == s.Id)?.SlotId, s.EntryTime, s.VehicleType)).ToArray();
        var result = ReservationAvailability.Evaluate(Demand(proposed), claims,
            visits, resources, snapshot.DynamicBackups.Where(b => b.VehicleType == proposed.VehicleType)
                .Select(b => new CapacityBackup(b.SpatialUnitId, b.Count)).ToArray(), facts.Quotas,
            command.EvaluatedAt, projection, command.EvaluatedAt >= proposed.ExpectedStartTime - allocationLead);
        if (!result.Allowed)
            throw new ReservationGateException(result.Rejection is "CAPACITY_CONFIGURATION_REQUIRED" or "OCCUPANCY_RECONCILIATION_REQUIRED" ? 503 : 409,
                result.Rejection!, "The requested capacity is unavailable or requires reconciliation.");
    }
}
