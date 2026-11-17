using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ReadOwnBookingHandler(ICurrentActor actor, IReservationBookingRepository bookings,
    ILogger<ReadOwnBookingHandler> logger)
    : IRequestHandler<ReadOwnBookingQuery, BookingView>
{
    public async Task<BookingView> Handle(ReadOwnBookingQuery query, CancellationToken ct)
    {
        var (_, account) = AdmissionActor.RequireDriver(actor);
        var result = await bookings.FindOwnBookingAsync(account, query.Id, ct);
        if (result is null)
        {
            logger.LogDebug("Own reservation detail returned {Outcome}.", "NotFound");
            throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found.");
        }

        logger.LogDebug("Own reservation detail returned {Outcome}.", "Found");
        return BookingContracts.View(result);
    }
}
