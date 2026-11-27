using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.PassPlans;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence.Entities;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class PassPlanRepository(
    ReservationDbContext db,
    IOperationsConcurrency? guards = null) : IPassPlanRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task<IReadOnlyList<ParkingPassPlan>> GetPlansBySiteAsync(Guid siteId, CancellationToken ct)
    {
        var rows = await db.ParkingPassPlans
            .AsNoTracking()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.ProductType)
            .ToListAsync(ct);

        return rows.Select(MapPlan).ToArray();
    }

    public async Task<ParkingPassPlan?> GetPlanAsync(Guid siteId, PassProductType productType, CancellationToken ct)
    {
        var typeText = productType.ToString();
        var row = await db.ParkingPassPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.ProductType == typeText, ct);

        return row is null ? null : MapPlan(row);
    }

    public async Task<ParkingPassPlan?> GetPlanByIdAsync(Guid siteId, Guid planId, CancellationToken ct)
    {
        var row = await db.ParkingPassPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Id == planId, ct);

        return row is null ? null : MapPlan(row);
    }

    public async Task<ParkingPassPlanRevision?> GetRevisionAsync(Guid planId, int revisionNumber, CancellationToken ct)
    {
        var row = await db.ParkingPassPlanRevisions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PlanId == planId && x.RevisionNumber == revisionNumber, ct);

        return row is null ? null : MapRevision(row);
    }

    public async Task<ParkingPassPlanRevision?> GetRevisionByIdempotencyKeyAsync(
        Guid planId,
        string idempotencyKey,
        CancellationToken ct)
    {
        var row = await db.ParkingPassPlanRevisions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PlanId == planId && x.IdempotencyKey == idempotencyKey, ct);

        return row is null ? null : MapRevision(row);
    }

    public async Task<string> GetEffectiveTimeZoneAsync(Guid siteId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var sitePolicyTimezone = await db.Set<OperationalPolicyRow>()
            .AsNoTracking()
            .Where(x => x.SiteId == siteId && x.IsActive && x.EffectiveFrom <= now &&
                        (x.EffectiveUntil == null || now < x.EffectiveUntil) &&
                        x.CancellationTimezone != null)
            .OrderByDescending(x => x.Version)
            .Select(x => x.CancellationTimezone)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(sitePolicyTimezone))
            return sitePolicyTimezone;

        var platformTimezone = await db.Set<OperationalPolicyRow>()
            .AsNoTracking()
            .Where(x => x.TenantId == null && x.SiteId == null && x.IsActive && x.CancellationTimezone != null)
            .OrderByDescending(x => x.Version)
            .Select(x => x.CancellationTimezone)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(platformTimezone) ? "Asia/Ho_Chi_Minh" : platformTimezone;
    }

    public async Task<Guid?> GetLocalSiteTenantIdAsync(Guid siteId, CancellationToken ct)
    {
        var tenantId = await db.StructureSiteStates.AsNoTracking()
            .Where(x => x.SiteId == siteId)
            .Select(x => (Guid?)x.TenantId)
            .FirstOrDefaultAsync(ct);
        if (tenantId is { } t1 && t1 != Guid.Empty) return t1;

        tenantId = await db.SearchFacilityProjections.AsNoTracking()
            .Where(x => x.SiteId == siteId)
            .Select(x => (Guid?)x.TenantId)
            .FirstOrDefaultAsync(ct);
        if (tenantId is { } t2 && t2 != Guid.Empty) return t2;

        tenantId = await db.SiteCapacityPools.AsNoTracking()
            .Where(x => x.SiteId == siteId)
            .Select(x => (Guid?)x.TenantId)
            .FirstOrDefaultAsync(ct);
        if (tenantId is { } t3 && t3 != Guid.Empty) return t3;

        tenantId = await db.Set<OperationalPolicyRow>().AsNoTracking()
            .Where(x => x.SiteId == siteId && x.TenantId != null)
            .Select(x => x.TenantId)
            .FirstOrDefaultAsync(ct);
        return tenantId is { } t4 && t4 != Guid.Empty ? t4 : null;
    }

    public async Task<IReadOnlySet<PassVehicleType>> GetLocalSupportedVehicleTypesAsync(Guid siteId, CancellationToken ct)
    {
        var result = new HashSet<PassVehicleType>();

        var projectionTypes = await db.SearchFacilityProjections.AsNoTracking()
            .Where(x => x.SiteId == siteId)
            .Select(x => x.SupportedVehicleTypes)
            .FirstOrDefaultAsync(ct);

        if (projectionTypes is not null)
        {
            foreach (var raw in projectionTypes)
            {
                if (Enum.TryParse<PassVehicleType>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
                    result.Add(parsed);
            }
        }

        var poolTypes = await db.SiteCapacityPools.AsNoTracking()
            .Where(x => x.SiteId == siteId && x.TotalCapacity > 0)
            .Select(x => x.VehicleType)
            .Distinct()
            .ToListAsync(ct);
        foreach (var raw in poolTypes)
        {
            if (Enum.TryParse<PassVehicleType>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
                result.Add(parsed);
        }

        return result;
    }

    public async Task<bool> HasLocalSiteEvidenceAsync(Guid siteId, CancellationToken ct)
    {
        return await db.StructureSiteStates.AsNoTracking().AnyAsync(x => x.SiteId == siteId, ct)
            || await db.SearchFacilityProjections.AsNoTracking().AnyAsync(x => x.SiteId == siteId, ct)
            || await db.SiteCapacityPools.AsNoTracking().AnyAsync(x => x.SiteId == siteId, ct)
            || await db.Set<OperationalPolicyRow>().AsNoTracking().AnyAsync(x => x.SiteId == siteId, ct)
            || await db.ParkingPassPlans.AsNoTracking().AnyAsync(x => x.SiteId == siteId, ct);
    }

    public async Task<IUnitOfWorkTransaction?> BeginSiteLockTransactionAsync(Guid siteId, CancellationToken ct)
    {
        if (!db.Database.IsRelational())
        {
            return null;
        }

        var transaction = await db.Database.BeginTransactionAsync(ct);
        if (guards is not null)
        {
            await guards.LockSiteAsync(siteId, ct);
        }
        return new PassPlanTransactionWrapper(transaction);
    }

    public async Task AddPlanAsync(ParkingPassPlan plan, CancellationToken ct)
    {
        var row = new ParkingPassPlanRow
        {
            Id = plan.Id,
            TenantId = plan.TenantId,
            SiteId = plan.SiteId,
            ProductType = plan.ProductType.ToString(),
            EntitlementType = plan.EntitlementType,
            IsEnabled = plan.IsEnabled,
            Status = plan.Status.ToString(),
            ActiveRevision = plan.ActiveRevision,
            DraftRevision = plan.DraftRevision,
            DraftPayload = SerializeDraft(plan.DraftSpec),
            CreatedBy = plan.CreatedBy,
            UpdatedBy = plan.UpdatedBy,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt
        };
        await db.ParkingPassPlans.AddAsync(row, ct);
    }

    public async Task UpdatePlanAsync(ParkingPassPlan plan, CancellationToken ct)
    {
        var row = db.ParkingPassPlans.Local.FirstOrDefault(x => x.Id == plan.Id)
            ?? await db.ParkingPassPlans.FirstOrDefaultAsync(x => x.Id == plan.Id, ct);

        if (row is null)
        {
            throw new InvalidOperationException($"Pass plan '{plan.Id}' was not found for update.");
        }

        row.IsEnabled = plan.IsEnabled;
        row.Status = plan.Status.ToString();
        row.ActiveRevision = plan.ActiveRevision;
        row.DraftRevision = plan.DraftRevision;
        row.DraftPayload = SerializeDraft(plan.DraftSpec);
        row.UpdatedBy = plan.UpdatedBy;
        row.UpdatedAt = plan.UpdatedAt;
    }

    public async Task AddRevisionAsync(ParkingPassPlanRevision revision, CancellationToken ct)
    {
        var row = new ParkingPassPlanRevisionRow
        {
            Id = revision.Id,
            PlanId = revision.PlanId,
            TenantId = revision.TenantId,
            SiteId = revision.SiteId,
            RevisionNumber = revision.RevisionNumber,
            ProductType = revision.ProductType.ToString(),
            ZoneId = revision.ZoneId,
            VehicleTypes = JsonSerializer.Serialize(revision.VehicleTypes.Select(v => v.ToString()).ToArray(), JsonOptions),
            OrdinaryCoverageRules = JsonSerializer.Serialize(
                new PassPlanCoverageJsonPayload(
                    revision.OrdinaryCoverageRules.DayCovered,
                    revision.OrdinaryCoverageRules.NightCovered,
                    revision.OrdinaryCoverageRules.ExcludedZoneIds),
                JsonOptions),
            ContractTimezone = revision.ContractTimezone,
            EffectiveFrom = revision.EffectiveFrom,
            EffectiveTo = revision.EffectiveTo,
            PublishNote = revision.PublishNote,
            IdempotencyKey = revision.IdempotencyKey,
            IdempotencyPayloadHash = revision.IdempotencyPayloadHash,
            PublishedBy = revision.PublishedBy,
            CreatedAt = revision.CreatedAt
        };
        await db.ParkingPassPlanRevisions.AddAsync(row, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static string? SerializeDraft(PassPlanDraftSpec? draft)
    {
        if (draft is null) return null;
        var payload = new PassPlanDraftJsonPayload(
            draft.ZoneId,
            draft.VehicleTypes.Select(v => v.ToString()).ToArray(),
            new PassPlanCoverageJsonPayload(
                draft.OrdinaryCoverageRules.DayCovered,
                draft.OrdinaryCoverageRules.NightCovered,
                draft.OrdinaryCoverageRules.ExcludedZoneIds),
            draft.IsAvailableOnly);
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static ParkingPassPlan MapPlan(ParkingPassPlanRow row)
    {
        var productType = Enum.Parse<PassProductType>(row.ProductType, ignoreCase: true);
        var status = Enum.Parse<PassPlanStatus>(row.Status, ignoreCase: true);
        PassPlanDraftSpec? draftSpec = null;
        if (!string.IsNullOrWhiteSpace(row.DraftPayload))
        {
            var payload = JsonSerializer.Deserialize<PassPlanDraftJsonPayload>(row.DraftPayload, JsonOptions);
            if (payload is not null)
            {
                draftSpec = new PassPlanDraftSpec(
                    payload.ZoneId,
                    ParseVehicleTypes(payload.VehicleTypes),
                    new OrdinaryCoverageRules(
                        payload.OrdinaryCoverageRules.DayCovered,
                        payload.OrdinaryCoverageRules.NightCovered,
                        payload.OrdinaryCoverageRules.ExcludedZoneIds ?? []),
                    payload.IsAvailableOnly);
            }
        }

        return ParkingPassPlan.Rehydrate(
            row.Id,
            row.TenantId,
            row.SiteId,
            productType,
            row.EntitlementType,
            row.IsEnabled,
            status,
            row.ActiveRevision,
            row.DraftRevision,
            draftSpec,
            row.CreatedBy,
            row.UpdatedBy,
            row.CreatedAt,
            row.UpdatedAt);
    }

    private static ParkingPassPlanRevision MapRevision(ParkingPassPlanRevisionRow row)
    {
        var productType = Enum.Parse<PassProductType>(row.ProductType, ignoreCase: true);
        var vehiclesRaw = JsonSerializer.Deserialize<string[]>(row.VehicleTypes, JsonOptions) ?? [];
        var coverageRaw = JsonSerializer.Deserialize<PassPlanCoverageJsonPayload>(row.OrdinaryCoverageRules, JsonOptions)
            ?? new PassPlanCoverageJsonPayload(true, true, []);

        return ParkingPassPlanRevision.Rehydrate(
            row.Id,
            row.PlanId,
            row.TenantId,
            row.SiteId,
            row.RevisionNumber,
            productType,
            row.ZoneId,
            ParseVehicleTypes(vehiclesRaw),
            new OrdinaryCoverageRules(
                coverageRaw.DayCovered,
                coverageRaw.NightCovered,
                coverageRaw.ExcludedZoneIds ?? []),
            row.ContractTimezone,
            row.EffectiveFrom,
            row.EffectiveTo,
            row.PublishNote,
            row.IdempotencyKey,
            row.IdempotencyPayloadHash,
            row.PublishedBy,
            row.CreatedAt);
    }

    private static IReadOnlyList<PassVehicleType> ParseVehicleTypes(IEnumerable<string> values) =>
        values.Select(v => Enum.Parse<PassVehicleType>(v, ignoreCase: true)).ToArray();
}
