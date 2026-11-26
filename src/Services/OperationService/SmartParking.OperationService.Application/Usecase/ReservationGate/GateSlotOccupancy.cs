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

        var lease = await gateSlots.ClaimAvailableAsync(session.TenantId, session.SiteId, session.VehicleType,
            slotId, null, session.ReservationId is not null, ct);
        if (lease.Slot.Id == slotId)
            return lease;

        await lease.DisposeAsync();
        throw new ReservationGateException(409, "ALLOCATION_UNAVAILABLE", "The requested slot could not be claimed.");
    }
}
