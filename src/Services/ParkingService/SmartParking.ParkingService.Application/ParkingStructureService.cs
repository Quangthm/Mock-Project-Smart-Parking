using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Application;

public sealed record OwnerScope(Guid UserId, Guid TenantId);

public interface ISlotOperatorAuthorizer
{
    Task<OwnerScope> ScopeAsync(Guid site, CancellationToken cancellationToken);
    Task<bool> IsAuthorizedAsync(OwnerScope actor, Guid site, CancellationToken cancellationToken);
}

public interface IParkingStructureRepository
{
    Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken cancellationToken);
    Task<ParkingStructure> ReadAsync(OwnerScope owner, Guid siteId, CommitmentSnapshot snapshot, CancellationToken cancellationToken);
    Task<SiteProfile> CreateAsync(OwnerScope owner, SiteProfile site, CancellationToken cancellationToken);
    Task<IParkingStructureEdit> BeginEditAsync(
        OwnerScope owner, Guid siteId, CommitmentSnapshot snapshot, Guid? operationId, CancellationToken cancellationToken);
}

// Application owns the use case; Persistence owns this short local transaction.
// Disposing an edit without committing rolls it back.
public interface IParkingStructureEdit : IAsyncDisposable
{
    ParkingStructure Structure { get; }
    Task CommitAsync(
        Guid synchronizationToken, StructureOutcome outcome,
        Guid? markedSlot, string? reason, CancellationToken cancellationToken);
}

public sealed class ParkingStructureService(
    IParkingStructureRepository repository,
    IOwnerAuthorizer owners,
    ISlotOperatorAuthorizer operators,
    IStructureCommitments commitments,
    IStructureSynchronizationStore synchronizationStore,
    StructureSynchronizationService synchronization,
    IOperationalLayoutReader? operationalLayouts = null,
    ILayoutCallerResolver? callerResolver = null)
{
    public async Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken ct = default)
    {
        await AuthorizeAsync(owner, null, ct);
        return await repository.ListAsync(owner, ct);
    }

    public async Task<SiteProfile> CreateAsync(
        OwnerScope owner, string code, string name, string address,
        decimal? latitude = null, decimal? longitude = null, CancellationToken ct = default)
    {
        await AuthorizeAsync(owner, null, ct);
        var structure = new ParkingStructure(
            new SiteProfile(Guid.NewGuid(), owner.TenantId, "", "", "", null, null, true, 0));
        structure.UpdateProfile(code, name, address, latitude, longitude);
        return await repository.CreateAsync(owner, structure.Site, ct);
    }

    public Task<ParkingStructure> GetAsync(OwnerScope owner, Guid siteId, CancellationToken ct = default)
        => ReadAsync(owner, siteId, false, ct);

    public async Task<OperationalLayoutProjectionDto> ReadOperationalAsync(
        Guid siteId, VehicleType? vehicleType, string? floor, CancellationToken ct)
    {
        if (callerResolver is null || operationalLayouts is null)
            throw new InvalidOperationException("Operational layout dependencies are not registered.");

        var caller = await callerResolver.ResolveAsync(siteId, ct);
        var visibility = caller.IsSiteTelemetryGranted
            ? LayoutVisibility.Telemetry
            : LayoutVisibility.Sanitized;
        var scope = ScopeFor(caller);

        // This read path never takes an edit hold or a ReservationService snapshot; the
        // projection reads parking_slots.reservation_state directly.
        OperationalLayoutReadResult result;
        try
        {
            result = await operationalLayouts.ReadAsync(scope, siteId, vehicleType, floor, ct);
        }
        catch (StructureException error) when (
            caller.Kind == LayoutCallerKind.Owner &&
            caller.TenantIds.Count > 0 &&
            error.Code == "SITE_NOT_FOUND")
        {
            // An owner scoped to another tenant still gets the sanitized public view.
            result = await operationalLayouts.ReadAsync(
                new LayoutReadScope(null, false), siteId, vehicleType, floor, ct);
            visibility = LayoutVisibility.Sanitized;
        }

        // Single choke point: this is the only projection return and it is always redacted.
        return OperationalLayoutRedaction.Apply(
            result.Layout,
            visibility,
            result.MaskedReservedCount,
            result.MaskedZoneReservedCounts);
    }

    private static LayoutReadScope ScopeFor(LayoutCaller caller) => caller.Kind switch
    {
        LayoutCallerKind.Admin => new(null, true),
        LayoutCallerKind.Owner when caller.TenantIds.Count > 0 => new(caller.TenantIds, true),
        LayoutCallerKind.Owner => new(null, false),
        LayoutCallerKind.Operator when caller.IsSiteTelemetryGranted && caller.TenantIds.Count > 0
            => new(caller.TenantIds, true),
        _ => new(null, false)
    };

    private async Task<ParkingStructure> ReadAsync(OwnerScope actor, Guid siteId, bool operational, CancellationToken ct)
    {
        await AuthorizeAsync(actor, operational ? siteId : null, ct);
        await using var gate = await LockSiteAsync(siteId, ct);
        await synchronization.RecoverSiteAsync(siteId, ct);

        // Reads also use a durable token: a process crash must not orphan their hold.
        var pending = await PrepareAsync(actor, siteId, null, ct);
        try
        {
            var snapshot = await commitments.AcquireAsync(actor.TenantId, siteId, pending.Token, ct);
            var result = await repository.ReadAsync(actor, siteId, snapshot, ct);
            await synchronization.PublishAsync(pending, ct);
            return result;
        }
        catch
        {
            // Nothing physical was committed on this path, so release the preparation
            // instead of leaving a durable row and a ReservationService hold behind.
            await TryAbortAsync(pending, ct);
            throw;
        }
    }

    public Task<Guid> AddUnitAsync(
        OwnerScope owner, Guid siteId, UnitType type, string name,
        Guid? parentId = null, int capacity = 0, CancellationToken ct = default)
        => EditAsync(owner, siteId, structure => structure.AddUnit(type, name, parentId, capacity), null, null, null, ct);

    public Task<Guid> AddSlotAsync(
        OwnerScope owner, Guid siteId, Guid unitId, string code, VehicleType vehicle,
        SlotType type = SlotType.STANDARD, CancellationToken ct = default)
        => EditAsync(owner, siteId, structure => structure.AddSlot(unitId, code, vehicle, type), null, null, null, ct);

    public Task<Guid> AddAccessPathAsync(
        OwnerScope owner, Guid siteId, string code, Guid? from, Guid? to,
        string? mapData = null, CancellationToken ct = default)
        => EditAsync(owner, siteId, structure => structure.AddAccessPath(code, from, to, mapData), null, null, null, ct);

    public Task ApplyAsync(
        OwnerScope owner, Guid siteId, StructureEdit edit, CancellationToken ct = default)
        => ChangeAsync(owner, siteId, aggregate => StructureEditHandler.Apply(aggregate, edit), ct);

    public Task ChangeAsync(
        OwnerScope owner, Guid siteId, Action<ParkingStructure> change,
        CancellationToken ct = default, Guid? operationId = null)
        => EditAsync(owner, siteId, structure =>
        {
            change(structure);
            return true;
        }, operationId, null, null, ct);

    public Task MarkOperationalBackupAsync(
        OwnerScope actor, Guid siteId, Guid slotId, string reason, CancellationToken ct)
        => SetOperationalBackupAsync(actor, siteId, slotId, true, reason, ct);

    public Task SetOperationalBackupAsync(
        OwnerScope actor, Guid siteId, Guid slotId, bool marked, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
        {
            throw new StructureException("INVALID_STRUCTURE", "Provide an operational reason (1–2000 characters).");
        }

        return EditAsync(actor, siteId, structure =>
        {
            structure.MarkBackup(slotId, marked);
            return true;
        }, null, slotId, reason.Trim(), ct);
    }

    private async Task<T> EditAsync<T>(
        OwnerScope actor, Guid siteId, Func<ParkingStructure, T> change,
        Guid? operationId, Guid? markedSlot, string? reason, CancellationToken ct)
    {
        await AuthorizeAsync(actor, markedSlot.HasValue ? siteId : null, ct);
        await using var gate = await LockSiteAsync(siteId, ct);
        await synchronization.RecoverSiteAsync(siteId, ct);

        var pending = await PrepareAsync(actor, siteId, operationId, ct);
        var committed = false;
        try
        {
            // No Parking transaction is open while Reservation prepares its hold.
            var snapshot = await commitments.AcquireAsync(actor.TenantId, siteId, pending.Token, ct);
            // Operational authority may be revoked while recovery/acquire waits.
            // Recheck before opening the local transaction; this does not claim
            // atomic revocation across UserService and ParkingService databases.
            if (markedSlot.HasValue)
                await AuthorizeAsync(actor, siteId, ct);

            StructureOutcome outcome;
            T result;

            // Row locks and the physical edit live only inside this local transaction.
            await using (var edit = await repository.BeginEditAsync(actor, siteId, snapshot, operationId, ct))
            {
                var structure = edit.Structure;
                if (structure.Site.TotalPhysicalCapacity != structure.Slots.Count)
                {
                    throw new StructureException(
                        "STRUCTURE_CONFLICT", "Reconcile declared physical capacity before editing.");
                }

                var originalSlots = structure.Slots.Select(slot => slot.Id).ToHashSet();
                var originalUnits = structure.Units.Select(unit => unit.Id).ToHashSet();

                result = change(structure);
                structure.ValidateCapacity();
                outcome = StructureOutcomeBuilder.Build(structure, originalSlots, originalUnits);

                // Commit physical data and the exact replay payload together.
                // If commit acknowledgement is lost, recovery reads the durable row
                // to decide between publishing this outcome and aborting preparation.
                await edit.CommitAsync(pending.Token, outcome, markedSlot, reason, ct);
            }

            // The local transaction has been disposed before the next HTTP request.
            committed = true;
            await synchronization.PublishAsync(pending with { State = "ready", Outcome = outcome }, ct);
            return result;
        }
        catch
        {
            // A committed edit keeps its durable ready row for exact-outcome recovery.
            // Any earlier failure never reached the physical commit, so release it now.
            if (!committed)
                await TryAbortAsync(pending, ct);
            throw;
        }
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

    private async Task<IAsyncDisposable> LockSiteAsync(Guid siteId, CancellationToken ct)
    {
        return await synchronizationStore.TryLockSiteAsync(siteId, ct)
            ?? throw new StructureException("STRUCTURE_BUSY", "Another request is managing this site.");
    }

    private async Task TryAbortAsync(StructureSynchronization pending, CancellationToken ct)
    {
        try
        {
            // Fences the preparation and releases its hold. The original failure is
            // the useful one, so a failing compensation must never replace it; the
            // durable row remains for the recovery worker.
            await synchronization.PublishAsync(pending, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch
        {
        }
    }

    private async Task<StructureSynchronization> PrepareAsync(
        OwnerScope actor, Guid siteId, Guid? operationId, CancellationToken ct)
    {
        var pending = new StructureSynchronization(
            Guid.NewGuid(), actor.TenantId, siteId, operationId, "preparing", null);
        await synchronizationStore.PrepareAsync(pending, ct);
        return pending;
    }
}
