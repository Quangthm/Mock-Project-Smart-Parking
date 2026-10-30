using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

public sealed class PostgresParkingStructureRepository(ParkingDbContext db) : IParkingStructureRepository
{
    private static string AuditState(ParkingStructure structure) =>
        JsonSerializer.Serialize(new { structure.Site, structure.Units, structure.Slots, structure.Paths, structure.BackupPolicies });

    private async Task EnsureTenantAsync(OwnerScope owner, CancellationToken ct)
    {
        if (!await db.Tenants.AnyAsync(x => x.Id == owner.TenantId && x.Status == "ACTIVE" && x.DeletedAt == null, ct))
            throw new StructureException("FORBIDDEN", "Active tenant is required.");
    }

    private static SiteProfile Profile(ParkingSiteEntity e) => new(e.Id, e.TenantId, e.Code, e.Name, e.Address,
        e.Latitude, e.Longitude, e.Status == "ACTIVE", e.TotalPhysicalCapacity, e.Status);

    public async Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await EnsureTenantAsync(owner, ct);
        var sites = await db.ParkingSites.AsNoTracking()
            .Where(x => x.TenantId == owner.TenantId && x.DeletedAt == null)
            .OrderBy(x => x.Code).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return sites.Select(Profile).ToList();
    }

    public async Task<SiteProfile> CreateAsync(OwnerScope owner, SiteProfile site, CancellationToken ct)
    {
        if (site.TenantId != owner.TenantId)
            throw new StructureException("FORBIDDEN", "Site must belong to the supplied tenant.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await EnsureTenantAsync(owner, ct);
        try
        {
            db.ParkingSites.Add(new ParkingSiteEntity
            {
                Id = site.Id, TenantId = site.TenantId, Code = site.Code, Name = site.Name,
                Address = site.Address, Latitude = site.Latitude, Longitude = site.Longitude,
                Status = site.Status, TotalPhysicalCapacity = site.TotalPhysicalCapacity
            });
            db.StructureChangeAudits.Add(new StructureChangeAuditEntity
            {
                Id = Guid.NewGuid(), SiteId = site.Id, ActorId = owner.UserId,
                AfterState = AuditState(new ParkingStructure(site)), CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return site;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new StructureException("DUPLICATE_IDENTIFIER", "Site code already exists in this tenant.");
        }
    }

    public async Task<ParkingStructure> ReadAsync(OwnerScope owner, Guid siteId,
        CommitmentSnapshot snapshot, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, ct);
        await EnsureTenantAsync(owner, ct);
        var structure = await LoadAsync(owner.TenantId, siteId, snapshot, ct);
        await tx.CommitAsync(ct);
        return structure;
    }

    public async Task<IParkingStructureEdit> BeginEditAsync(OwnerScope owner, Guid siteId,
        CommitmentSnapshot snapshot, Guid? operationId, CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await EnsureTenantAsync(owner, ct);
            if (operationId is { } operation)
            {
                var existing = await db.StructureOperations.FirstOrDefaultAsync(x =>
                    x.Id == operation && x.SiteId == siteId && x.TenantId == owner.TenantId &&
                    x.ActorId == owner.UserId, ct);
                if (existing is null) throw new StructureException("FORBIDDEN", "Operation is not in scope.");
                if (existing.PhysicalCommitted)
                    throw new StructureException("STRUCTURE_CONFLICT", "This physical operation has already committed.");
            }
            var structure = await LoadAsync(owner.TenantId, siteId, snapshot, ct);
            return new ParkingStructureEdit(db, tx, owner, siteId, operationId, structure);
        }
        catch { await tx.DisposeAsync(); throw; }
    }

    private async Task<ParkingStructure> LoadAsync(Guid tenantId, Guid siteId,
        CommitmentSnapshot? snapshot, CancellationToken ct)
    {
        var site = await db.ParkingSites.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == siteId && x.TenantId == tenantId && x.DeletedAt == null, ct)
            ?? throw new StructureException("SITE_NOT_FOUND", "Parking site was not found in this tenant.");
        var units = await db.SpatialUnits.AsNoTracking().Where(x =>
            x.SiteId == siteId && x.TenantId == tenantId && x.DeletedAt == null).ToListAsync(ct);
        var slots = await db.ParkingSlotsFull.AsNoTracking().Where(x =>
            x.SiteId == siteId && x.TenantId == tenantId && x.DeletedAt == null).ToListAsync(ct);
        var paths = await db.ParkingAccessPaths.AsNoTracking().Where(x =>
            x.SiteId == siteId && x.TenantId == tenantId && x.DeletedAt == null).ToListAsync(ct);
        var backups = await db.StructureBackupPolicies.AsNoTracking().Where(x => x.SiteId == siteId).ToListAsync(ct);
        return new(Profile(site),
            units.Select(x => new SpatialUnit(x.Id, x.ParentId, x.Path, Enum.Parse<UnitType>(x.UnitType), x.Name, x.MaxCapacity)),
            slots.Select(x => new ParkingSlot(x.Id, x.SpatialUnitId, x.Code,
                Enum.Parse<VehicleType>(x.VehicleType), Enum.Parse<SlotType>(x.SlotType),
                x.PhysicalState switch { "AVAILABLE" or "OCCUPIED" => OperationalStatus.OPERATIONAL, "UNAVAILABLE" => OperationalStatus.BLOCKED, _ => Enum.Parse<OperationalStatus>(x.PhysicalState) },
                x.PhysicalState == "OCCUPIED", x.Coordinates3D, x.Features, x.ReservationState)),
            paths.Select(x => new AccessPath(x.Id, x.Code, x.FromUnitId, x.ToUnitId, x.MapData)),
            snapshot?.ProtectedSlots, snapshot?.HasLiveCommitments ?? false,
            backups.Select(x => new BackupPolicy(x.UnitId, Enum.Parse<VehicleType>(x.VehicleType), x.Count)),
            snapshot?.Capacities);
    }

    private sealed class ParkingStructureEdit(ParkingDbContext db, IDbContextTransaction tx,
        OwnerScope owner, Guid siteId, Guid? operationId, ParkingStructure structure) : IParkingStructureEdit
    {
        private readonly HashSet<Guid> oldUnits = structure.Units.Select(x => x.Id).ToHashSet();
        private readonly HashSet<Guid> oldSlots = structure.Slots.Select(x => x.Id).ToHashSet();
        private readonly HashSet<Guid> oldPaths = structure.Paths.Select(x => x.Id).ToHashSet();
        private readonly string before = AuditState(structure);
        public ParkingStructure Structure => structure;

        public async Task CommitAsync(Guid synchronizationToken, StructureOutcome outcome,
            Guid? markedSlot, string? reason, CancellationToken ct)
        {
            try
            {
                var site = await db.ParkingSites.FirstAsync(x => x.Id == structure.Site.Id, ct);
                site.Code = structure.Site.Code; site.Name = structure.Site.Name; site.Address = structure.Site.Address;
                site.Latitude = structure.Site.Latitude; site.Longitude = structure.Site.Longitude;
                site.Status = structure.Site.Status; site.TotalPhysicalCapacity = structure.Slots.Count;
                var now = DateTimeOffset.UtcNow;
                await db.SpatialUnits.Where(x => oldUnits.Contains(x.Id) && !structure.Units.Select(y => y.Id).Contains(x.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeletedAt, now), ct);
                await db.ParkingSlotsFull.Where(x => oldSlots.Contains(x.Id) && !structure.Slots.Select(y => y.Id).Contains(x.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeletedAt, now), ct);
                await db.ParkingAccessPaths.Where(x => oldPaths.Contains(x.Id) && !structure.Paths.Select(y => y.Id).Contains(x.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeletedAt, now), ct);

                foreach (var u in structure.Units.OrderBy(x => x.Path.Length))
                {
                    var entity = await db.SpatialUnits.FirstOrDefaultAsync(x => x.Id == u.Id, ct);
                    if (entity is null) db.SpatialUnits.Add(new SpatialUnitEntity
                    { Id = u.Id, TenantId = site.TenantId, SiteId = site.Id, ParentId = u.ParentId, Path = u.Path, UnitType = u.Type.ToString(), Name = u.Name, MaxCapacity = u.MaxCapacity });
                    else { entity.Name = u.Name; entity.MaxCapacity = u.MaxCapacity; entity.DeletedAt = null; }
                }
                foreach (var s in structure.Slots)
                {
                    var entity = await db.ParkingSlotsFull.FirstOrDefaultAsync(x => x.Id == s.Id, ct);
                    if (entity is null) db.ParkingSlotsFull.Add(new ParkingSlotFullEntity
                    { Id = s.Id, TenantId = site.TenantId, SiteId = site.Id, SpatialUnitId = s.UnitId, Code = s.Code, VehicleType = s.VehicleType.ToString(), SlotType = s.Type.ToString(), PhysicalState = s.IsPhysicallyOccupied ? "OCCUPIED" : "AVAILABLE", Coordinates3D = s.Coordinates3D, Features = s.Features, ReservationState = s.ReservationState });
                    else { entity.SpatialUnitId = s.UnitId; entity.Code = s.Code; entity.VehicleType = s.VehicleType.ToString(); entity.SlotType = s.Type.ToString(); entity.Coordinates3D = s.Coordinates3D; entity.Features = s.Features; entity.ReservationState = s.ReservationState; entity.DeletedAt = null; }
                }
                foreach (var p in structure.Paths)
                {
                    var entity = await db.ParkingAccessPaths.FirstOrDefaultAsync(x => x.Id == p.Id, ct);
                    if (entity is null) db.ParkingAccessPaths.Add(new ParkingAccessPathEntity
                    { Id = p.Id, TenantId = site.TenantId, SiteId = site.Id, Code = p.Code, FromUnitId = p.FromUnitId, ToUnitId = p.ToUnitId, MapData = p.MapData });
                    else { entity.Code = p.Code; entity.FromUnitId = p.FromUnitId; entity.ToUnitId = p.ToUnitId; entity.MapData = p.MapData; entity.DeletedAt = null; }
                }
                await db.StructureBackupPolicies.Where(x => x.SiteId == siteId).ExecuteDeleteAsync(ct);
                db.StructureBackupPolicies.AddRange(structure.BackupPolicies.Select(p => new StructureBackupPolicyEntity
                { Id = Guid.NewGuid(), SiteId = siteId, UnitId = p.UnitId, VehicleType = p.VehicleType.ToString(), Count = p.Count }));
                if (operationId is { } applied)
                    await db.StructureOperations.Where(x => x.Id == applied).ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.PhysicalCommitted, true).SetProperty(x => x.UpdatedAt, now), ct);
                db.StructureChangeAudits.Add(new StructureChangeAuditEntity
                { Id = Guid.NewGuid(), SiteId = siteId, ActorId = owner.UserId, OperationId = operationId, BeforeState = before, AfterState = AuditState(structure), CreatedAt = now });
                if (markedSlot is { } marked)
                    db.StructureSlotAudits.Add(new StructureSlotAuditEntity
                    { Id = Guid.NewGuid(), SiteId = siteId, SlotId = marked, ActorId = owner.UserId, Action = structure.Slots.Single(x => x.Id == marked).ReservationState == "BACKUP" ? "MARK_BACKUP" : "RELEASE_BACKUP", Reason = reason, CreatedAt = now });
                var sync = await db.StructureSynchronizations.FirstOrDefaultAsync(x =>
                    x.Token == synchronizationToken && x.State == "preparing" && x.SiteId == siteId && x.TenantId == owner.TenantId, ct);
                if (sync is null) throw new StructureException("STRUCTURE_CONFLICT", "Synchronization preparation is missing.");
                sync.State = "ready"; sync.Outcome = JsonSerializer.Serialize(outcome, StructureSynchronizationJson.Options); sync.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            { throw new StructureException("DUPLICATE_IDENTIFIER", "Structure identifier already exists."); }
        }

        public async ValueTask DisposeAsync() => await tx.DisposeAsync();
    }
}
