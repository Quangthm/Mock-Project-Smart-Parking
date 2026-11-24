using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Tests.TestDoubles;

// Test-only doubles for the maintenance closure apply/cancel handlers. Only the members the
// handlers actually exercise are implemented; the rest fail loudly so an unexpected call is
// visible rather than silently satisfied.
internal sealed class FakeMaintenanceRepository : IStructureRepository
{
    private readonly Dictionary<Guid, StructureMaintenanceClosureRow> _closures = [];

    public IReadOnlyList<MaintenanceCommitmentConflict> Conflicts { get; set; } = [];
    public int AddMaintenanceClosureCalls { get; private set; }
    public Guid? RequestedConflictsUnitId { get; private set; }
    public Guid? RequestedConflictsSlotId { get; private set; }
    public IReadOnlySet<Guid>? RequestedConflictsScopedUnitIds { get; private set; }
    public IReadOnlySet<Guid>? RequestedConflictsScopedSlotIds { get; private set; }
    public DateTimeOffset? RequestedConflictsStart { get; private set; }
    public DateTimeOffset? RequestedConflictsEnd { get; private set; }
    public DateTimeOffset? RequestedConflictsNow { get; private set; }

    public StructureSiteStateRow? SiteState { get; set; }

    public void Seed(StructureMaintenanceClosureRow closure) => _closures[closure.WindowId] = closure;

    public StructureMaintenanceClosureRow? FindStored(Guid windowId) =>
        _closures.TryGetValue(windowId, out var closure) ? closure : null;

    public Task<StructureMaintenanceClosureRow?> MaintenanceClosureAsync(Guid windowId, CancellationToken ct) =>
        Task.FromResult(FindStored(windowId));

    public void AddMaintenanceClosure(StructureMaintenanceClosureRow closure)
    {
        AddMaintenanceClosureCalls++;
        _closures[closure.WindowId] = closure;
    }

    public Task<IReadOnlyList<MaintenanceCommitmentConflict>> FindMaintenanceConflictsAsync(
        Guid tenantId, Guid siteId, Guid? unitId, Guid? slotId,
        IReadOnlySet<Guid> scopedUnitIds, IReadOnlySet<Guid> scopedSlotIds,
        DateTimeOffset start, DateTimeOffset end, DateTimeOffset now, CancellationToken ct)
    {
        RequestedConflictsUnitId = unitId;
        RequestedConflictsSlotId = slotId;
        RequestedConflictsScopedUnitIds = scopedUnitIds;
        RequestedConflictsScopedSlotIds = scopedSlotIds;
        RequestedConflictsStart = start;
        RequestedConflictsEnd = end;
        RequestedConflictsNow = now;
        return Task.FromResult(Conflicts);
    }

    public Task<StructureEditHoldRow?> HoldAsync(Guid siteId, CancellationToken ct) => throw new NotSupportedException();
    public Task<StructureReleaseReceiptRow?> ReceiptAsync(Guid token, CancellationToken ct) => throw new NotSupportedException();
    public Task<StructureSiteStateRow?> StateAsync(Guid siteId, CancellationToken ct) => Task.FromResult(SiteState);
    public Task<IReadOnlyList<SiteCapacityPoolRow>> PoolsAsync(Guid tenantId, Guid siteId, CancellationToken ct) => throw new NotSupportedException();
    public Task<HoldSnapshot> SnapshotAsync(Guid tenantId, Guid siteId, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    public Task<StructureImpactResponse> ImpactAsync(Guid tenantId, Guid siteId, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    public void AddHold(StructureEditHoldRow hold) => throw new NotSupportedException();
    public void RemoveHold(StructureEditHoldRow hold) => throw new NotSupportedException();
    public void AddReceipt(StructureReleaseReceiptRow receipt) => throw new NotSupportedException();
    public void AddState(StructureSiteStateRow state) => throw new NotSupportedException();
}

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

// A settable TimeProvider so handler tests can distinguish "acknowledged once" from
// "re-acknowledged with a fresh timestamp".
internal sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
