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
        using var accepted = command.CurrentProjectionMinutes is null ? JsonDocument.Parse(proposed.AcceptedOperationalPolicySnapshot!) : null;
        var projection = TimeSpan.FromMinutes(command.CurrentProjectionMinutes ??
            accepted!.RootElement.GetProperty("Values").GetProperty("OCCUPANCY_VALIDITY_TIMESPAN").GetInt32());
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
            facts.MaxReservationQuota, command.EvaluatedAt, projection, unresolved, ct);
        if (!result.Allowed)
            throw new ReservationGateException(result.Indeterminate ? 503 : 409,
                result.Rejection!, "The requested capacity is unavailable or requires reconciliation.");
    }
}
