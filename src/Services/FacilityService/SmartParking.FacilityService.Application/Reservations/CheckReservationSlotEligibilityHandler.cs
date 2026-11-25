using MediatR;

namespace SmartParking.FacilityService.Application.Reservations;

public sealed class CheckReservationSlotEligibilityHandler(IReservationDependencyRepository repository)
    : IRequestHandler<CheckReservationSlotEligibilityQuery, ReservationSlotEligibility>
{
    public async Task<ReservationSlotEligibility> Handle(CheckReservationSlotEligibilityQuery query, CancellationToken ct) =>
        await repository.CheckSlotEligibilityAsync(query.SlotId, query.Start, query.End, ct)
        ?? new(query.SlotId, "INELIGIBLE", null, null);
}
