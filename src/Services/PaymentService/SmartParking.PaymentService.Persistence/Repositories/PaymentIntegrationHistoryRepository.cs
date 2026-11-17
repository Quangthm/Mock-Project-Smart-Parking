using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Repositories;

/// <summary>
/// Reuses the existing <c>policy_revisions</c> table to persist accepted payment
/// integration configurations. No schema change is introduced.
/// </summary>
public sealed class PaymentIntegrationHistoryRepository : IPaymentIntegrationHistoryRepository
{
    // PostgreSQL CHECK constraints from 04-payment-pricing-service-db.sql:33,37:
    // CHECK (policy_type IN ('TARIFF', 'SUBSCRIPTION', 'OVERSTAY_PENALTY', 'SPECIAL_EVENT'))
    // CHECK (scope_type IN ('GLOBAL', 'TENANT', 'SITE', 'ZONE'))
    // Under Schema Freeze, ScopeType is 'GLOBAL' and PolicyType is 'TARIFF'.
    public const string PolicyType = "TARIFF";
    public const string ScopeType = "GLOBAL";

    // Fallback serialization for non-relational providers (tests / InMemory) that cannot
    // take the PostgreSQL advisory lock. Keyed by policy id.
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> InProcessGates = new();

    private readonly PaymentDbContext _dbContext;

    public PaymentIntegrationHistoryRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentIntegrationRevision?> GetLatestAsync(
        Guid policyId,
        CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.PolicyRevisions
            .AsNoTracking()
            .Where(p => p.PolicyId == policyId && p.PolicyType == PolicyType)
            .OrderByDescending(p => p.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new PaymentIntegrationRevision(
                row.PolicyId,
                row.RevisionNumber,
                row.PolicyValuesJson,
                row.CreatedBy,
                row.CreatedAt);
    }

    public async Task AppendAsync(
        Guid policyId,
        string policyValuesJson,
        Guid createdBy,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsRelational())
        {
            await AppendWithDatabaseLockAsync(policyId, policyValuesJson, createdBy, createdAt, cancellationToken);
            return;
        }

        // Provider-agnostic fallback used by unit tests. Production always runs the
        // PostgreSQL advisory-lock path above.
        var gate = InProcessGates.GetOrAdd(policyId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await AppendInternalAsync(policyId, policyValuesJson, createdBy, createdAt, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task AppendWithDatabaseLockAsync(
        Guid policyId,
        string policyValuesJson,
        Guid createdBy,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        // Serialize revision allocation for one policy inside a transaction. A transaction-scoped
        // advisory lock also covers the empty-table case, where SELECT ... FOR UPDATE locks no row
        // and two concurrent MAX+1 allocations would both collide on the unique index.
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            await _dbContext.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtext({0})::bigint)",
                new object[] { policyId.ToString("D") },
                cancellationToken);

            await AppendInternalAsync(policyId, policyValuesJson, createdBy, createdAt, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task AppendInternalAsync(
        Guid policyId,
        string policyValuesJson,
        Guid createdBy,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var maxRevision = await _dbContext.PolicyRevisions
            .Where(p => p.PolicyId == policyId && p.PolicyType == PolicyType)
            .Select(p => (int?)p.RevisionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var revision = new PolicyRevision
        {
            Id = Guid.NewGuid(),
            PolicyId = policyId,
            PolicyType = PolicyType,
            RevisionNumber = maxRevision + 1,
            EffectiveFrom = createdAt,
            EffectiveTo = null,
            ScopeType = ScopeType,
            ScopeId = null,
            PolicyValuesJson = policyValuesJson,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
        };

        await _dbContext.PolicyRevisions.AddAsync(revision, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}