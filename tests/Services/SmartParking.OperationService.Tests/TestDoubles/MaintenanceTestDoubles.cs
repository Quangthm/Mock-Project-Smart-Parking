using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Tests.TestDoubles;

internal sealed class StubOperationsConcurrency : IOperationsConcurrency
{
    public int LockSiteCalls { get; private set; }

    public Task LockSiteAsync(Guid siteId, CancellationToken ct)
    {
        LockSiteCalls++;
        return Task.CompletedTask;
    }

    public Task LockVehicleAsync(string identity, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class StubUnitOfWork : IUnitOfWork
{
    public StubUnitOfWorkTransaction Transaction { get; } = new();
    public int SaveChangesCalls { get; private set; }

    public IReservationRepository Reservations => throw new NotSupportedException();
    public IParkingSessionRepository ParkingSessions => throw new NotSupportedException();
    public ISlotAllocationRepository SlotAllocations => throw new NotSupportedException();

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IUnitOfWorkTransaction>(Transaction);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return Task.FromResult(0);
    }
}

internal sealed class StubUnitOfWorkTransaction : IUnitOfWorkTransaction
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

internal sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
