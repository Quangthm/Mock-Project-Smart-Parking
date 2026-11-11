using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class PaymentSucceededHandler(
    IReservationGateStore store,
    IUnitOfWork unitOfWork,
    IOperatorGateSlotClaims gateSlots) : IPaymentSucceededHandler
{
    public async Task HandleAsync(PaymentSucceededMessage message, CancellationToken ct)
    {
        IReadOnlyList<Guid> slotsToRelease;
        await using (var transaction = await unitOfWork.BeginTransactionAsync(ct))
        {
            slotsToRelease = await store.CompletePaymentAsync(
                message.TenantId,
                message.SiteId,
                message.ReferenceType,
                message.ReferenceId,
                message.OccurredAt,
                ct);
            await transaction.CommitAsync(ct);
        }

        foreach (var slotId in slotsToRelease)
            await gateSlots.ReleaseAsync(message.TenantId, message.SiteId, slotId, ct);
    }
}
