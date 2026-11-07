using SmartParking.FacilityService.Application.Common.Helpers;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

public sealed class StructureUnitOfWork(
    IParkingStructureRepository repository,
    IOwnerAuthorizer owners,
    ISlotOperatorAuthorizer operators,
    IStructureCommitments commitments,
    IStructureSynchronizationStore synchronizationStore,
    IStructureSynchronizationService synchronization) : IStructureUnitOfWork
{
    public async Task<IStructureEditSession> BeginEditAsync(
        OwnerScope owner, Guid siteId, CancellationToken ct,
        Guid? operationId = null, Guid? markedSlot = null, string? reason = null)
    {
        if (markedSlot.HasValue && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000))
        {
            throw new StructureException("INVALID_STRUCTURE", "Provide an operational reason (1–2000 characters).");
        }

        await AuthorizeAsync(owner, markedSlot.HasValue ? siteId : null, ct);
        var gate = await synchronizationStore.TryLockSiteAsync(siteId, ct)
            ?? throw new StructureException("STRUCTURE_BUSY", "Another request is managing this site.");

        await synchronization.RecoverSiteAsync(siteId, ct);

        var pending = new StructureSynchronization(
            Guid.NewGuid(), owner.TenantId, siteId, operationId, "preparing", null);
        await synchronizationStore.PrepareAsync(pending, ct);

        CommitmentSnapshot snapshot;
        try
        {
            // No Parking transaction is open while Reservation prepares its hold.
            snapshot = await commitments.AcquireAsync(owner.TenantId, siteId, pending.Token, ct);

            // Operational authority may be revoked while recovery/acquire waits.
            // Recheck before opening the local transaction.
            if (markedSlot.HasValue)
            {
                await AuthorizeAsync(owner, siteId, ct);
            }
        }
        catch
        {
            await TryAbortAsync(pending, ct);
            await gate.DisposeAsync();
            throw;
        }

        IParkingStructureEdit edit;
        try
        {
            // Row locks and the physical edit live only inside this local transaction.
            edit = await repository.BeginEditAsync(owner, siteId, snapshot, operationId, ct);
        }
        catch
        {
            await TryAbortAsync(pending, ct);
            await gate.DisposeAsync();
            throw;
        }

        return new StructureEditSession(
            edit,
            gate,
            pending,
            synchronization,
            markedSlot,
            reason);
    }

    public async Task RecoverAsync(OwnerScope owner, Guid siteId, CancellationToken ct)
    {
        await AuthorizeAsync(owner, null, ct);
        await using var gate = await synchronizationStore.TryLockSiteAsync(siteId, ct)
            ?? throw new StructureException("STRUCTURE_BUSY", "Another request is managing this site.");
        await synchronization.RecoverSiteAsync(siteId, ct);
    }

    private async Task AuthorizeAsync(OwnerScope actor, Guid? operationalSite, CancellationToken ct)
    {
        var allowed = operationalSite is { } site
            ? await operators.IsAuthorizedAsync(actor, site, ct)
            : await owners.IsOwnerAsync(actor, ct);

        if (!allowed)
        {
            throw new StructureException("FORBIDDEN", "Current parking management access is required.");
        }
    }

    private async Task TryAbortAsync(StructureSynchronization pending, CancellationToken ct)
    {
        try
        {
            await synchronization.PublishAsync(pending, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch
        {
        }
    }

    private sealed class StructureEditSession : IStructureEditSession
    {
        private readonly IParkingStructureEdit _edit;
        private readonly IAsyncDisposable _gate;
        private readonly StructureSynchronization _pending;
        private readonly IStructureSynchronizationService _synchronization;
        private readonly Guid? _markedSlot;
        private readonly string? _reason;
        private readonly HashSet<Guid> _originalSlots;
        private readonly HashSet<Guid> _originalUnits;
        private bool _committed;
        private bool _disposed;

        public StructureEditSession(
            IParkingStructureEdit edit,
            IAsyncDisposable gate,
            StructureSynchronization pending,
            IStructureSynchronizationService synchronization,
            Guid? markedSlot,
            string? reason)
        {
            _edit = edit;
            _gate = gate;
            _pending = pending;
            _synchronization = synchronization;
            _markedSlot = markedSlot;
            _reason = reason;
            Structure = edit.Structure;
            _originalSlots = edit.Structure.Slots.Select(s => s.Id).ToHashSet();
            _originalUnits = edit.Structure.Units.Select(u => u.Id).ToHashSet();
        }

        public ParkingStructure Structure { get; }

        public async Task CommitAsync(CancellationToken ct)
        {
            if (_committed) throw new InvalidOperationException("Session has already been committed.");
            if (_disposed) throw new ObjectDisposedException(nameof(StructureEditSession));

            Structure.ValidateCapacity();
            var outcome = StructureOutcomeBuilder.Build(Structure, _originalSlots, _originalUnits);

            await _edit.CommitAsync(_pending.Token, outcome, _markedSlot, _reason?.Trim(), ct);
            _committed = true;
            await _edit.DisposeAsync();

            await _synchronization.PublishAsync(_pending with { State = "ready", Outcome = outcome }, ct);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            if (!_committed)
            {
                try
                {
                    await _edit.DisposeAsync();
                }
                catch
                {
                }

                try
                {
                    await _synchronization.PublishAsync(_pending, CancellationToken.None);
                }
                catch
                {
                }
            }

            try
            {
                await _gate.DisposeAsync();
            }
            catch
            {
            }
        }
    }
}
