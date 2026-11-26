using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class CheckAdmissionDeadlineHandler(IReservationRequestRepository requests, IReservationBookingRepository bookings,
    IUnitOfWork work, IAdmissionMessages? messages = null) : IRequestHandler<CheckAdmissionDeadline, ReservationDeadlineResult>
{
    public async Task<ReservationDeadlineResult> Handle(CheckAdmissionDeadline command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(command.RequestId, ct);
        if (request is null || request.OverallDeadline != command.ExpectedAt ||
            request.State is not ("CHECKING" or "EVALUATING")) return ReservationDeadlineResult.Obsolete;
        var now = await bookings.DatabaseNowAsync(ct);
        if (now < command.ExpectedAt) return ReservationDeadlineResult.Early(command.ExpectedAt - now);
        request.ExpireAdmission();
        await (messages ?? throw new InvalidOperationException("Deadline messaging unavailable."))
            .WakeAsync(request.Id, request.OverallDeadline, ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ReservationDeadlineResult.Completed;
    }
}
