using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReadOwnBookingQuery(Guid Id) : IRequest<BookingView>;
