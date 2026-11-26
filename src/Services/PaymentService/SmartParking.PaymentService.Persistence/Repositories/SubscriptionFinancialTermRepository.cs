using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Repositories;

public sealed class SubscriptionFinancialTermRepository(PaymentDbContext db) : ISubscriptionFinancialTermRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IPaymentDbTransaction?> BeginTransactionAsync(
        Guid siteId,
        Guid planId,
        CancellationToken ct = default)
    {
        if (!db.Database.IsRelational())
        {
            return null;
        }

        var tx = await db.Database.BeginTransactionAsync(ct);
        var lockKey = $"pass-financial-terms:{siteId:N}:{planId:N}";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            ct);
        return new PaymentDbTransactionWrapper(tx);
    }

    public Task<SubscriptionFinancialTerm?> GetBySiteAndPlanAsync(Guid siteId, Guid planId, CancellationToken ct = default) =>
        db.SubscriptionFinancialTerms
            .Include(t => t.Revisions)
            .FirstOrDefaultAsync(t => t.SiteId == siteId && t.PlanId == planId, ct);

    public async Task<IReadOnlyList<SubscriptionFinancialTerm>> GetActiveBySiteAsync(
        Guid siteId,
        Guid? planId,
        CancellationToken ct = default)
    {
        var query = db.SubscriptionFinancialTerms
            .AsNoTracking()
            .Include(t => t.Revisions)
            .Where(t => t.SiteId == siteId && t.IsActive);

        if (planId.HasValue)
        {
            query = query.Where(t => t.PlanId == planId.Value);
        }

        return await query
            .OrderBy(t => t.CreatedAt)
            .ToArrayAsync(ct);
    }

    public Task<SubscriptionFinancialTermRevision?> GetRevisionByIdempotencyKeyAsync(
        Guid financialTermId,
        string idempotencyKey,
        CancellationToken ct = default) =>
        db.SubscriptionFinancialTermRevisions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.FinancialTermId == financialTermId && r.IdempotencyKey == idempotencyKey,
                ct);

    public Task<SubscriptionFinancialTermRevision?> GetRevisionAsync(
        Guid financialTermId,
        int revisionNumber,
        CancellationToken ct = default) =>
        db.SubscriptionFinancialTermRevisions
            .FirstOrDefaultAsync(
                r => r.FinancialTermId == financialTermId && r.RevisionNumber == revisionNumber,
                ct);

    public async Task AddTermAsync(SubscriptionFinancialTerm term, CancellationToken ct = default) =>
        await db.SubscriptionFinancialTerms.AddAsync(term, ct);

    public Task UpdateTermAsync(SubscriptionFinancialTerm term, CancellationToken ct = default)
    {
        if (db.Entry(term).State == EntityState.Detached)
        {
            db.SubscriptionFinancialTerms.Update(term);
        }

        return Task.CompletedTask;
    }

    public async Task AddRevisionAsync(SubscriptionFinancialTermRevision revision, CancellationToken ct = default) =>
        await db.SubscriptionFinancialTermRevisions.AddAsync(revision, ct);

    public Task UpdateRevisionAsync(SubscriptionFinancialTermRevision revision, CancellationToken ct = default)
    {
        if (db.Entry(revision).State == EntityState.Detached)
        {
            db.SubscriptionFinancialTermRevisions.Update(revision);
        }

        return Task.CompletedTask;
    }

    public async Task AddSubscriptionPolicyRevisionAsync(
        Guid financialTermId,
        Guid siteId,
        Guid planId,
        int planRevision,
        int financialTermRevision,
        string productType,
        string currency,
        IReadOnlyList<SubscriptionVehiclePrice> vehiclePrices,
        SubscriptionRefundPolicy refundPolicy,
        DateTimeOffset effectiveFrom,
        Guid createdBy,
        CancellationToken ct = default)
    {
        var policySnapshotJson = JsonSerializer.Serialize(new
        {
            financialTermId,
            siteId,
            planId,
            planRevision,
            financialTermRevision,
            productType,
            currency,
            vehiclePrices = vehiclePrices.Select(vp => new
            {
                vehicleType = vp.VehicleType,
                fullPeriodPrice = vp.FullPeriodPrice
            }),
            refundPolicy = new
            {
                voluntaryBeforeStartPercent = refundPolicy.VoluntaryBeforeStartPercent,
                voluntaryInPeriodPercent = refundPolicy.VoluntaryInPeriodPercent,
                facilityClosurePercent = refundPolicy.FacilityClosurePercent,
                roundingMode = refundPolicy.RoundingMode
            }
        }, JsonOptions);

        await db.PolicyRevisions.AddAsync(new PolicyRevision
        {
            Id = Guid.NewGuid(),
            PolicyId = financialTermId,
            PolicyType = "SUBSCRIPTION",
            RevisionNumber = financialTermRevision,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = null,
            ScopeType = "SITE",
            ScopeId = siteId,
            PolicyValuesJson = policySnapshotJson,
            CreatedBy = createdBy,
            CreatedAt = effectiveFrom
        }, ct);
    }

    public async Task CloseActivePolicyRevisionsAsync(Guid policyId, DateTimeOffset effectiveTo, CancellationToken ct = default)
    {
        var openRevisions = await db.PolicyRevisions
            .Where(p => p.PolicyId == policyId && p.PolicyType == "SUBSCRIPTION" && p.EffectiveTo == null)
            .ToArrayAsync(ct);

        foreach (var rev in openRevisions)
        {
            rev.EffectiveTo = effectiveTo;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
