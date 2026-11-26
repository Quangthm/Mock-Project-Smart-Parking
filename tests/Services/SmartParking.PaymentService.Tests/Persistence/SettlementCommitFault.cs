using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SmartParking.PaymentService.Tests.Persistence;

internal sealed class SettlementCommitFault : DbTransactionInterceptor
{
    public bool Enabled { get; set; }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Enabled) throw new IOException("Injected failure before committing the collection and its outbox or disposition.");
        return ValueTask.FromResult(result);
    }
}
