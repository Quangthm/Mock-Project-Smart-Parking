using MediatR;

namespace SmartParking.FacilityService.Application.Reservations;

public sealed record ResolveReservationSlotQuery(Guid SlotId) : IRequest<BookingFacts>;
