using Microsoft.EntityFrameworkCore.Storage;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

namespace SmartParking.PaymentService.Persistence.Repositories;

internal sealed class PaymentDbTransactionWrapper(IDbContextTransaction inner) : IPaymentDbTransaction
{
    public Task CommitAsync(CancellationToken ct = default) => inner.CommitAsync(ct);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
