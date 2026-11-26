using MediatR;

namespace SmartParking.FacilityService.Application.Reservations;

public sealed record CheckReservationSlotEligibilityQuery(Guid SlotId, DateTimeOffset Start, DateTimeOffset End)
    : IRequest<ReservationSlotEligibility>;
