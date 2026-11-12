using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Application.Reservations;

// Runs in a fresh scope after a failed worker transaction, never saves a poisoned DbContext.
public sealed class RecordReservationWorkFailureHandler(IReservationRequestRepository requests,
    IReservationBookingRepository bookings, IUnitOfWork work) : IRequestHandler<RecordReservationWorkFailureCommand>
{
    public async Task Handle(RecordReservationWorkFailureCommand command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(command.RequestId, ct);
        if (request is null || request.State is not ("PREPARING" or "BOOKING")) return;
        // Advance the existing worker cadence so a poisoned first page cannot starve later work.
        // This is scheduling, not a claim that an unknown remote effect failed or a new attempt.
        request.Defer((await bookings.DatabaseNowAsync(ct)).AddSeconds(1), "WORKFLOW_RECOVERY_PENDING");
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
