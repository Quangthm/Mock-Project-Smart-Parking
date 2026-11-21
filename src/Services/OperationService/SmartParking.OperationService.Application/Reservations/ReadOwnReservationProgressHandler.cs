using MediatR;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ReadOwnReservationProgressHandler(ICurrentActor actor, IReservationRequestRepository requests,
    IReservationBookingRepository bookings) : IRequestHandler<ReadOwnReservationProgress, ReservationProgress>
{
    public async Task<ReservationProgress> Handle(ReadOwnReservationProgress query, CancellationToken ct)
    {
        var (_, account) = AdmissionActor.RequireDriver(actor);
        var request = await requests.FindOwnAsync(account, query.RequestId, ct)
            ?? throw new ReservationGateException(404, "REQUEST_NOT_FOUND", "Request was not found.");
        var booking = request.ReservationId is Guid id ? await bookings.FindOrderProvisioningAsync(id, ct) : null;
        return ReservationProgress.From(request, booking);
    }
}
