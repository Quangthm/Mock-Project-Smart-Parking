using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;
using SmartParking.ParkingService.Persistence.Entities;

namespace SmartParking.ParkingService.Persistence;

public sealed class PostgresParkingStructureRepository(ParkingDbContext db) : IParkingStructureRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken ct)
    {
        await EnsureTenantAsync(owner.TenantId, ct);
        return await db.ParkingSites.AsNoTracking()
            .Where(site => site.TenantId == owner.TenantId && site.DeletedAt == null)
            .OrderBy(site => site.SiteCode)
            .Select(site => ToProfile(site))
            .ToListAsync(ct);
    }

    public async Task<SiteProfile> CreateAsync(OwnerScope owner, SiteProfile site, CancellationToken ct)
    {
        if (site.TenantId != owner.TenantId)
            throw new StructureException("FORBIDDEN", "Site must belong to the supplied tenant.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await EnsureTenantAsync(owner.TenantId, ct);
        db.ParkingSites.Add(new ParkingSiteEntity
        {
            Id = site.Id,
            TenantId = site.TenantId,
            SiteCode = site.Code,
            Name = site.Name,
            Address = site.Address,
            Latitude = site.Latitude,
            Longitude = site.Longitude,
            Status = site.Status,
            TotalPhysicalCapacity = site.TotalPhysicalCapacity,
        });
        db.StructureChangeAudits.Add(new StructureChangeAuditEntity
        {
            Id = Guid.NewGuid(),
            SiteId = site.Id,
            ActorId = owner.UserId,
            AfterState = JsonSerializer.Serialize(new ParkingStructure(site), JsonOptions),
        });

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return site;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new StructureException("DUPLICATE_IDENTIFIER", "Site code already exists in this tenant.");
        }
    }

    public async Task<ParkingStructure> ReadAsync(
        OwnerScope owner, Guid siteId, CommitmentSnapshot snapshot, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, ct);
        await EnsureTenantAsync(owner.TenantId, ct);
        var structure = await LoadAsync(owner.TenantId, siteId, false, snapshot, ct);
        await transaction.CommitAsync(ct);
        return structure;
    }

    public async Task<IParkingStructureEdit> BeginEditAsync(
        OwnerScope owner, Guid siteId, CommitmentSnapshot snapshot, Guid? operationId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        try
        {
            await EnsureTenantAsync(owner.TenantId, ct);
            if (operationId is { } operation)
            {
                var operationRow = await db.StructureOperations
                    .SingleOrDefaultAsync(row => row.Id == operation &&
                        row.SiteId == siteId && row.TenantId == owner.TenantId &&
                        row.ActorId == owner.UserId, ct);
                if (operationRow is null)
                    throw new StructureException("FORBIDDEN", "Operation is not in scope.");
                if (operationRow.PhysicalCommitted)
                    throw new StructureException("STRUCTURE_CONFLICT", "This physical operation has already committed.");
            }

            var structure = await LoadAsync(owner.TenantId, siteId, true, snapshot, ct);
            return new ParkingStructureEdit(db, transaction, owner, siteId, operationId, structure);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private async Task EnsureTenantAsync(Guid tenantId, CancellationToken ct)
    {
        if (!await db.Tenants.AsNoTracking().AnyAsync(
                tenant => tenant.Id == tenantId &&
                    tenant.Status == "ACTIVE" && tenant.DeletedAt == null, ct))
            throw new StructureException("FORBIDDEN", "Active tenant is required.");
    }

    private async Task<ParkingStructure> LoadAsync(
        Guid tenantId, Guid siteId, bool write, CommitmentSnapshot snapshot, CancellationToken ct)
    {
        var site = await db.ParkingSites
            .SingleOrDefaultAsync(item => item.Id == siteId &&
                item.TenantId == tenantId && item.DeletedAt == null, ct);
        if (site is null)
            throw new StructureException("SITE_NOT_FOUND", "Parking site was not found in this tenant.");

        var units = await db.SpatialUnits
            .Where(item => item.SiteId == siteId && item.TenantId == tenantId && item.DeletedAt == null)
            .ToListAsync(ct);
        var slots = await db.ParkingSlots
            .Where(item => item.SiteId == siteId && item.TenantId == tenantId && item.DeletedAt == null)
            .ToListAsync(ct);
        var paths = await db.ParkingAccessPaths
            .Where(item => item.SiteId == siteId && item.TenantId == tenantId && item.DeletedAt == null)
            .ToListAsync(ct);
        var backups = await db.StructureBackupPolicies
            .Where(item => item.SiteId == siteId)
            .ToListAsync(ct);

        return new ParkingStructure(
            ToProfile(site),
            units.Select(item => new SpatialUnit(
                item.Id, item.ParentId, item.Path, Enum.Parse<UnitType>(item.UnitType),
                item.Name, item.MaxCapacity)),
            slots.Select(ToSlot),
            paths.Select(item => new AccessPath(
                item.Id, item.PathCode, item.FromUnitId, item.ToUnitId, item.MapData)),
            snapshot.ProtectedSlots,
            snapshot.HasLiveCommitments,
            backups.Select(item => new BackupPolicy(
                item.UnitId, Enum.Parse<VehicleType>(item.VehicleType), item.Count)),
            snapshot.Capacities);
    }

    private static SiteProfile ToProfile(ParkingSiteEntity site) =>
        new(site.Id, site.TenantId, site.SiteCode, site.Name, site.Address,
            site.Latitude, site.Longitude, site.Status == "ACTIVE",
            site.TotalPhysicalCapacity, site.Status);

    private static ParkingSlot ToSlot(ParkingSlotEntity slot)
    {
        var operationalStatus = slot.PhysicalState switch
        {
            "AVAILABLE" or "OCCUPIED" => OperationalStatus.OPERATIONAL,
            "UNAVAILABLE" => OperationalStatus.BLOCKED,
            _ => Enum.Parse<OperationalStatus>(slot.PhysicalState),
        };
        return new ParkingSlot(
            slot.Id,
            slot.SpatialUnitId,
            slot.SlotCode,
            Enum.Parse<VehicleType>(slot.SupportedVehicleType),
            Enum.Parse<SlotType>(slot.SlotType),
            operationalStatus,
            slot.PhysicalState == "OCCUPIED",
            slot.Coordinates3D,
            slot.Features,
            slot.ReservationState);
    }

    private static string PhysicalState(ParkingSlot slot) =>
        slot.IsPhysicallyOccupied
            ? "OCCUPIED"
            : slot.OperationalStatus == OperationalStatus.BLOCKED
                ? "UNAVAILABLE"
                : slot.OperationalStatus.ToString();

    private sealed class ParkingStructureEdit(
        ParkingDbContext db,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        OwnerScope owner,
        Guid siteId,
        Guid? operationId,
        ParkingStructure structure) : IParkingStructureEdit
    {
        private readonly HashSet<Guid> oldUnits = structure.Units.Select(item => item.Id).ToHashSet();
        private readonly HashSet<Guid> oldSlots = structure.Slots.Select(item => item.Id).ToHashSet();
        private readonly HashSet<Guid> oldPaths = structure.Paths.Select(item => item.Id).ToHashSet();
        private readonly string before = JsonSerializer.Serialize(structure, JsonOptions);

        public ParkingStructure Structure => structure;

        public async Task CommitAsync(
            Guid synchronizationToken, StructureOutcome outcome,
            Guid? markedSlot, string? reason, CancellationToken ct)
        {
            try
            {
                var site = await db.ParkingSites.SingleAsync(item => item.Id == siteId, ct);
                site.SiteCode = structure.Site.Code;
                site.Name = structure.Site.Name;
                site.Address = structure.Site.Address;
                site.Latitude = structure.Site.Latitude;
                site.Longitude = structure.Site.Longitude;
                site.Status = structure.Site.Status;
                site.TotalPhysicalCapacity = structure.Slots.Count;

                var currentUnits = await db.SpatialUnits
                    .Where(item => item.SiteId == siteId && item.DeletedAt == null)
                    .ToListAsync(ct);
                foreach (var removed in currentUnits.Where(item => !structure.Units.Any(unit => unit.Id == item.Id)))
                    removed.DeletedAt = DateTimeOffset.UtcNow;
                foreach (var unit in structure.Units)
                {
                    var entity = currentUnits.SingleOrDefault(item => item.Id == unit.Id);
                    if (entity is null)
                    {
                        db.SpatialUnits.Add(new SpatialUnitEntity
                        {
                            Id = unit.Id, TenantId = site.TenantId, SiteId = siteId,
                            ParentId = unit.ParentId, Path = unit.Path, UnitType = unit.Type.ToString(),
                            Name = unit.Name, MaxCapacity = unit.MaxCapacity,
                        });
                    }
                    else
                    {
                        entity.ParentId = unit.ParentId;
                        entity.Path = unit.Path;
                        entity.UnitType = unit.Type.ToString();
                        entity.Name = unit.Name;
                        entity.MaxCapacity = unit.MaxCapacity;
                    }
                }

                var currentSlots = await db.ParkingSlots
                    .Where(item => item.SiteId == siteId && item.DeletedAt == null)
                    .ToListAsync(ct);
                foreach (var removed in currentSlots.Where(item => !structure.Slots.Any(slot => slot.Id == item.Id)))
                    removed.DeletedAt = DateTimeOffset.UtcNow;
                foreach (var slot in structure.Slots)
                {
                    var entity = currentSlots.SingleOrDefault(item => item.Id == slot.Id);
                    if (entity is null)
                    {
                        db.ParkingSlots.Add(ToEntity(site, slot));
                    }
                    else
                    {
                        entity.SpatialUnitId = slot.UnitId;
                        entity.SlotCode = slot.Code;
                        entity.SupportedVehicleType = slot.VehicleType.ToString();
                        entity.SlotType = slot.Type.ToString();
                        entity.PhysicalState = PhysicalState(slot);
                        entity.Coordinates3D = slot.Coordinates3D;
                        entity.Features = slot.Features;
                        entity.ReservationState = slot.ReservationState;
                    }
                }

                var currentPaths = await db.ParkingAccessPaths
                    .Where(item => item.SiteId == siteId && item.DeletedAt == null)
                    .ToListAsync(ct);
                foreach (var removed in currentPaths.Where(item => !structure.Paths.Any(path => path.Id == item.Id)))
                    removed.DeletedAt = DateTimeOffset.UtcNow;
                foreach (var path in structure.Paths)
                {
                    var entity = currentPaths.SingleOrDefault(item => item.Id == path.Id);
                    if (entity is null)
                    {
                        db.ParkingAccessPaths.Add(new ParkingAccessPathEntity
                        {
                            Id = path.Id, TenantId = site.TenantId, SiteId = siteId,
                            PathCode = path.Code, FromUnitId = path.FromUnitId,
                            ToUnitId = path.ToUnitId, MapData = path.MapData,
                        });
                    }
                }

                var policies = await db.StructureBackupPolicies
                    .Where(item => item.SiteId == siteId).ToListAsync(ct);
                db.StructureBackupPolicies.RemoveRange(policies);
                db.StructureBackupPolicies.AddRange(structure.BackupPolicies.Select(policy =>
                    new StructureBackupPolicyEntity
                    {
                        Id = Guid.NewGuid(), SiteId = siteId, UnitId = policy.UnitId,
                        VehicleType = policy.VehicleType.ToString(), Count = policy.Count,
                    }));

                if (operationId is { } operation)
                {
                    var operationRow = await db.StructureOperations
                        .SingleAsync(item => item.Id == operation, ct);
                    operationRow.PhysicalCommitted = true;
                }

                db.StructureChangeAudits.Add(new StructureChangeAuditEntity
                {
                    Id = Guid.NewGuid(), SiteId = siteId, ActorId = owner.UserId,
                    OperationId = operationId, BeforeState = before,
                    AfterState = JsonSerializer.Serialize(structure, JsonOptions),
                });
                if (markedSlot is { } slotId)
                {
                    db.StructureSlotAudits.Add(new StructureSlotAuditEntity
                    {
                        Id = Guid.NewGuid(), SiteId = siteId, SlotId = slotId,
                        ActorId = owner.UserId, Action = structure.Slots.Single(slot => slot.Id == slotId)
                            .ReservationState == "BACKUP" ? "MARK_BACKUP" : "RELEASE_BACKUP",
                        Reason = reason ?? "",
                    });
                }

                var synchronization = await db.StructureSynchronizations
                    .SingleOrDefaultAsync(item => item.Token == synchronizationToken &&
                        item.SiteId == siteId && item.TenantId == owner.TenantId &&
                        item.State == "preparing", ct);
                if (synchronization is null)
                    throw new StructureException("STRUCTURE_CONFLICT", "Synchronization preparation is missing.");
                synchronization.State = "ready";
                synchronization.Outcome = JsonSerializer.Serialize(outcome, JsonOptions);

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                throw new StructureException("DUPLICATE_IDENTIFIER", "Structure identifier already exists.");
            }
        }

        public async ValueTask DisposeAsync() => await transaction.DisposeAsync();

        private static ParkingSlotEntity ToEntity(ParkingSiteEntity site, ParkingSlot slot) =>
            new()
            {
                Id = slot.Id, TenantId = site.TenantId, SiteId = site.Id,
                SpatialUnitId = slot.UnitId, SlotCode = slot.Code,
                SupportedVehicleType = slot.VehicleType.ToString(), SlotType = slot.Type.ToString(),
                PhysicalState = PhysicalState(slot), Coordinates3D = slot.Coordinates3D,
                Features = slot.Features, ReservationState = slot.ReservationState,
            };
    }
}
