using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReadOwnBookingsQuery(int PageSize = 20, DateTimeOffset? Before = null, Guid? BeforeId = null)
    : IRequest<IReadOnlyList<BookingView>>;
