using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SmartParking.OperationService.Tests;

internal sealed class QuoteCommitFault : DbTransactionInterceptor
{
    public bool Enabled { get; set; }
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Enabled) throw new IOException("Injected failure after saving the hold and its outbox.");
        return ValueTask.FromResult(result);
    }
}
