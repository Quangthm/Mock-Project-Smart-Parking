using MediatR;

namespace SmartParking.FacilityService.Application.Reservations;

public sealed record ReadBookingFactsQuery(Guid SiteId, Guid SlotId) : IRequest<BookingFacts>;
