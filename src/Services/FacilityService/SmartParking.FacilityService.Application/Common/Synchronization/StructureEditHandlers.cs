using SmartParking.FacilityService.Application.Common.Helpers;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Application.Reservations;
using MediatR;

namespace SmartParking.FacilityService.Application.Common.Synchronization;

public sealed class StructureEditHandlers(
    IParkingStructureRepository repository,
    IOwnerAuthorizer owners,
    ISlotOperatorAuthorizer operators,
    IStructureCommitments commitments,
    IStructureSynchronizationRepository synchronizationStore,
    IMediator synchronization,
    IReservationDependencyRepository reservationDependencies)
    : IRequestHandler<BeginStructureEditCommand, IStructureEditSession>, IRequestHandler<RecoverAuthorizedStructureCommand>
{
    public Task<IStructureEditSession> Handle(BeginStructureEditCommand command, CancellationToken ct) =>
        BeginEditAsync(command.Owner, command.SiteId, ct, command.OperationId, command.MarkedSlot, command.Reason);

    public Task Handle(RecoverAuthorizedStructureCommand command, CancellationToken ct) =>
        RecoverAsync(command.Owner, command.SiteId, ct);

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

        try
        {
            if (await reservationDependencies.HasHeldAsync(siteId, ct))
                throw new StructureException("STRUCTURE_BUSY", "Reservation acceptance is using this site's configuration.");
            await synchronization.Send(new RecoverStructureSiteCommand(siteId), ct);
        }
        catch { await gate.DisposeAsync(); throw; }

        var pending = new StructureSynchronization(
            Guid.NewGuid(), owner.TenantId, siteId, operationId, "preparing", null);
        try { await synchronizationStore.PrepareAsync(pending, ct); }
        catch { await gate.DisposeAsync(); throw; }

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
        await synchronization.Send(new RecoverStructureSiteCommand(siteId), ct);
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
            await synchronization.Send(new PublishStructureOutcomeCommand(pending), ct);
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
        private readonly IMediator _synchronization;
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
            IMediator synchronization,
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

            await _synchronization.Send(new PublishStructureOutcomeCommand(_pending with { State = "ready", Outcome = outcome }), ct);
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
                    await _synchronization.Send(new PublishStructureOutcomeCommand(_pending), CancellationToken.None);
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
