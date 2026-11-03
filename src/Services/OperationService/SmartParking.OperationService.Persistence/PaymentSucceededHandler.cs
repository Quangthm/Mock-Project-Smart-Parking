using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Persistence;

public sealed class PaymentSucceededHandler(
    IReservationGateStore store,
    IUnitOfWork unitOfWork) : IPaymentSucceededHandler
{
    public async Task HandleAsync(PaymentSucceededMessage message, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await store.CompletePaymentAsync(
            message.TenantId,
            message.SiteId,
            message.ReferenceType,
            message.ReferenceId,
            message.OccurredAt,
            ct);
        await transaction.CommitAsync(ct);
    }
}
