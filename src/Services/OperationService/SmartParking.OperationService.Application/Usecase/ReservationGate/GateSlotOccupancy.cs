using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class GateSlotOccupancy(
    IReservationAvailabilityRepository availability,
    IOperatorGateSlotClaims gateSlots)
{
    public async Task<IOperatorGateSlotLease> ClaimAsync(ParkingSessionRow session, Guid slotId,
        DateTimeOffset at, CancellationToken ct)
    {
        var facts = await availability.ReadPhysicalAsync(session, at, ct);
        var decision = facts.EvaluatePhysical(slotId, session, at);
        if (!decision.Allowed)
            throw new ReservationGateException(decision.Indeterminate ? 503 : 409, decision.Rejection!,
                "The requested slot is unavailable or requires reconciliation.");

        // Facility can consume a RESERVED marker only when this reservation has a claim on this bay.
        var allowReservedSlot = session.ReservationId is { } owner &&
            (facts.Allocations.Any(a => a.ReservationId == owner && a.SlotId == slotId &&
                (a.ParkingSessionId == null || a.ParkingSessionId == session.Id)) ||
             facts.Reservations.Any(r => r.Id == owner && (r.ActiveClaimSlotId ?? r.TargetSlotId) == slotId &&
                 r.ExpectedStartTime <= at && at < (r.ClaimEndTime ?? r.ExpectedEndTime)));
        var lease = await gateSlots.ClaimAvailableAsync(session.TenantId, session.SiteId, session.VehicleType,
            slotId, null, allowReservedSlot, ct);
        if (lease.Slot.Id == slotId)
            return lease;

        await lease.DisposeAsync();
        throw new ReservationGateException(409, "ALLOCATION_UNAVAILABLE", "The requested slot could not be claimed.");
    }
}
