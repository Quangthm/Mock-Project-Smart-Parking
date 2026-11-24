using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Tests.TestDoubles;

// Test transaction that only records commits. The in-memory provider has no real transactions, so
// the handler's short transactions are asserted by commit count instead.
internal sealed class ReconciliationTransaction : IUnitOfWorkTransaction
{
    public int CommitCalls { get; private set; }
    public bool Disposed { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        CommitCalls++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}