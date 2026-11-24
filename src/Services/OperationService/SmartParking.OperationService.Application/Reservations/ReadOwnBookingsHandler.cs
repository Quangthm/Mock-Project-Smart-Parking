using MediatR;
using Microsoft.Extensions.Logging;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ReadOwnBookingsHandler(ICurrentActor actor, IReservationBookingRepository bookings,
    ILogger<ReadOwnBookingsHandler> logger)
    : IRequestHandler<ReadOwnBookingsQuery, IReadOnlyList<BookingView>>
{
    public async Task<IReadOnlyList<BookingView>> Handle(ReadOwnBookingsQuery query, CancellationToken ct)
    {
        var (_, account) = AdmissionActor.RequireDriver(actor);
        var results = await bookings.ListOwnBookingsAsync(account, query.PageSize, query.Before, query.BeforeId, ct, query.From, query.To);
        logger.LogDebug("Own reservations returned {ResultCount} bookings for page size {PageSize}.",
            results.Count, query.PageSize);
        return results.Select(BookingContracts.View).ToArray();
    }
}
