using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class AdvanceReservationAuthorizationHandler(IReservationRequestRepository requests,
    IUnitOfWork work, IAdmissionMessages? saga = null)
    : IRequestHandler<AdvanceReservationAuthorizationCommand>
{
    public async Task Handle(AdvanceReservationAuthorizationCommand command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(command.RequestId, ct);
        if (request?.AccountCheck is null || request.State != "CHECKING" || saga is null) return;
        await saga.WakeAsync(request.Id, ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
