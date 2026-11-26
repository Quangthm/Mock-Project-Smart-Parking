using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Policies.Commands;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Queries;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Policies;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Infrastructure.Policies;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests.Policies;

public class LotPoliciesSecurityAndIntegrityTests
{
    private sealed class StubOwnerIdentityClient(Guid userId, params Guid[] tenantIds) : IOwnerIdentityClient
    {
        public Task<OwnerIdentityScope> GetOwnerScopeAsync(string permission = "PARKING_MANAGE", CancellationToken ct = default)
        {
            return Task.FromResult(new OwnerIdentityScope(userId, tenantIds));
        }
    }

    private static ReservationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new ReservationDbContext(options);

        // Seed platform policy
        db.Set<OperationalPolicyRow>().Add(new OperationalPolicyRow
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            SiteId = null,
            Version = 1,
            IsActive = true,
            PaymentHoldDurationMinutes = 15,
            ArrivalLateToleranceMinutes = 15,
            ArrivalLateToleranceEnabled = true,
            MinReservationDurationMinutes = 15,
            MaxReservationDurationHours = 24,
            ReservationProtectionWindowHours = 4,
            AllocationLeadTimeMinutes = 60,
            OccupancyValidityTimespanHours = 4,
            MaxAdvanceBookingDays = 7,
            InterBookingBufferMinutes = 0
        });
        db.SaveChanges();

        return db;
    }

    [Fact]
    public async Task SiteOwnershipValidator_WhenSiteNotFound_ThrowsLotPolicyNotFoundException()
    {
        using var db = CreateInMemoryDb();
        var client = new StubOwnerIdentityClient(Guid.NewGuid(), Guid.NewGuid());
        var validator = new SiteOwnershipValidator(db, client);

        var nonExistentSiteId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<LotPolicyNotFoundException>(() =>
            validator.ValidateOwnershipAsync(Guid.NewGuid(), nonExistentSiteId, CancellationToken.None));

        Assert.Equal("SITE_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task SiteOwnershipValidator_WhenTenantMismatch_ThrowsCrossLotAccessException()
    {
        using var db = CreateInMemoryDb();
        var siteId = Guid.NewGuid();
        var siteTenantId = Guid.NewGuid();
        var otherOwnerTenantId = Guid.NewGuid();

        db.StructureSiteStates.Add(new StructureSiteStateRow
        {
            SiteId = siteId,
            TenantId = siteTenantId,
            Active = true
        });
        await db.SaveChangesAsync();

        var client = new StubOwnerIdentityClient(Guid.NewGuid(), otherOwnerTenantId);
        var validator = new SiteOwnershipValidator(db, client);

        await Assert.ThrowsAsync<CrossLotAccessException>(() =>
            validator.ValidateOwnershipAsync(Guid.NewGuid(), siteId, CancellationToken.None));
    }

    [Fact]
    public async Task SiteOwnershipValidator_WhenTenantMatches_ReturnsAuthoritativeTenantId()
    {
        using var db = CreateInMemoryDb();
        var siteId = Guid.NewGuid();
        var siteTenantId = Guid.NewGuid();

        db.StructureSiteStates.Add(new StructureSiteStateRow
        {
            SiteId = siteId,
            TenantId = siteTenantId,
            Active = true
        });
        await db.SaveChangesAsync();

        var client = new StubOwnerIdentityClient(Guid.NewGuid(), siteTenantId);
        var validator = new SiteOwnershipValidator(db, client);

        var resolvedTenantId = await validator.ValidateOwnershipAsync(Guid.NewGuid(), siteId, CancellationToken.None);

        Assert.Equal(siteTenantId, resolvedTenantId);
    }

    [Fact]
    public async Task ScheduledPublish_EnforcesNonOverlappingGistRanges()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        // 1. Publish revision 1 immediately
        await repo.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            PaymentHoldDurationMinutes = 10,
            MinReservationDurationMinutes = 30
        }, null, CancellationToken.None);

        var pub1 = await repo.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, "Initial release", "key-1", CancellationToken.None);
        Assert.Equal(1, pub1.NewRevision);
        Assert.Equal("ACTIVE", pub1.Status);

        // 2. Save draft and publish with SCHEDULED mode 2 days in the future
        var scheduledAt = DateTimeOffset.UtcNow.AddDays(2);
        await repo.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            PaymentHoldDurationMinutes = 20,
            MinReservationDurationMinutes = 45
        }, scheduledAt, CancellationToken.None);

        var pub2 = await repo.PublishDraftAsync(siteId, tenantId, actorId, "SCHEDULED", scheduledAt, "Future release", "key-2", CancellationToken.None);
        Assert.Equal(2, pub2.NewRevision);
        Assert.Equal("SCHEDULED", pub2.Status);

        // 3. Inspect database rows for non-overlapping ranges
        var rows = await db.Set<OperationalPolicyRow>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.Version)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        var rev1 = rows[0];
        var rev2 = rows[1];

        // Rev 1 should be clamped to scheduledAt
        Assert.True(rev1.IsActive);
        Assert.Equal(scheduledAt, rev1.EffectiveUntil);

        // Rev 2 should start at scheduledAt
        Assert.True(rev2.IsActive);
        Assert.Equal(scheduledAt, rev2.EffectiveFrom);
        Assert.Null(rev2.EffectiveUntil);

        // Range 1 is [rev1.EffectiveFrom, scheduledAt), Range 2 is [scheduledAt, null) -> 0 overlap!
        Assert.True(rev1.EffectiveFrom < rev1.EffectiveUntil);
        Assert.Equal(rev1.EffectiveUntil, rev2.EffectiveFrom);

        // 4. Now publish an IMMEDIATE policy while a future SCHEDULED policy exists:
        // Must safely supersede the scheduled policy (IsActive = false) and archive rev1, keeping 0 overlaps!
        await repo.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            PaymentHoldDurationMinutes = 25,
            MinReservationDurationMinutes = 60
        }, null, CancellationToken.None);

        var pub3 = await repo.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, "Superseding release", "key-3", CancellationToken.None);
        Assert.Equal(3, pub3.NewRevision);
        Assert.Equal("ACTIVE", pub3.Status);

        var allRows = await db.Set<OperationalPolicyRow>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.Version)
            .ToListAsync();

        Assert.Equal(3, allRows.Count);
        var r1 = allRows[0];
        var r2 = allRows[1];
        var r3 = allRows[2];

        // Rev 1 must now be archived (IsActive = false, EffectiveUntil <= now)
        Assert.False(r1.IsActive);
        Assert.NotNull(r1.EffectiveUntil);

        // Rev 2 (scheduled) must be superseded (IsActive = false)
        Assert.False(r2.IsActive);

        // Rev 3 must be currently active (IsActive = true, EffectiveUntil = null)
        Assert.True(r3.IsActive);
        Assert.Null(r3.EffectiveUntil);
    }

    [Fact]
    public async Task Publish_EnforcesDownstreamInvariants_NonNullToleranceAndAuthoritativeTenantId()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await repo.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            PaymentHoldDurationMinutes = 15,
            ArrivalLateToleranceEnabled = null // Unset explicitly
        }, null, CancellationToken.None);

        var pub = await repo.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, "Checking invariants", "key-inv", CancellationToken.None);

        var publishedRow = await db.Set<OperationalPolicyRow>().FirstAsync(x => x.SiteId == siteId && x.Version == pub.NewRevision);

        // TenantId must be the authoritative tenantId passed in
        Assert.Equal(tenantId, publishedRow.TenantId);

        // ArrivalLateToleranceEnabled must NEVER be null
        Assert.NotNull(publishedRow.ArrivalLateToleranceEnabled);

        // PublishedBy and PublishNote must be recorded
        Assert.Equal(actorId, publishedRow.PublishedBy);
        Assert.Equal("Checking invariants", publishedRow.PublishNote);
    }

    [Fact]
    public async Task Publish_WithSameIdempotencyKeyAndSamePayload_ReplaysResult()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const string idempotencyKey = "idemp-same-key";

        await repo.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            PaymentHoldDurationMinutes = 15
        }, null, CancellationToken.None);

        var first = await repo.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, "Note A", idempotencyKey, CancellationToken.None);
        Assert.False(first.IsReplayed);

        // Attempt second publish with same key and same payload
        var second = await repo.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, "Note A", idempotencyKey, CancellationToken.None);
        Assert.True(second.IsReplayed);
        Assert.Equal(first.NewRevision, second.NewRevision);
    }

    [Fact]
    public async Task Publish_WithSameIdempotencyKeyAndDifferentPayload_ThrowsMismatchException()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const string idempotencyKey = "idemp-diff-key";

        await repo.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            PaymentHoldDurationMinutes = 15
        }, null, CancellationToken.None);

        await repo.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, "Note A", idempotencyKey, CancellationToken.None);

        // Create new draft
        await repo.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            PaymentHoldDurationMinutes = 25
        }, null, CancellationToken.None);

        // Attempt publish with same key but different note/mode/schedule
        await Assert.ThrowsAsync<IdempotencyPayloadMismatchException>(() =>
            repo.PublishDraftAsync(siteId, tenantId, actorId, "SCHEDULED", DateTimeOffset.UtcNow.AddDays(5), "Different Note", idempotencyKey, CancellationToken.None));
    }

    [Fact]
    public async Task SaveDraft_WithNestedPoliciesEnvelope_SavesSuccessfully()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var request = new SavePolicyDraftRequest
        {
            Policies = new PolicyDraftConfig
            {
                MinReservationDurationMinutes = 30,
                MaxReservationDurationHours = 48,
                PaymentHoldDurationMinutes = 20
            }
        };

        var config = request.ToDraftConfig();
        var saved = await repo.SaveDraftAsync(siteId, tenantId, actorId, config, null, CancellationToken.None);

        var retrieved = await repo.GetDraftAsync(siteId, CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Contains("\"minReservationDurationMinutes\":30", retrieved.PayloadJson);
        Assert.Contains("\"paymentHoldDurationMinutes\":20", retrieved.PayloadJson);
    }

    [Fact]
    public async Task GetDraft_WhenNoDraftExists_ReturnsNull()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();

        var draft = await repo.GetDraftAsync(siteId, CancellationToken.None);
        Assert.Null(draft);
    }

    [Fact]
    public async Task Publish_WithSameIdempotencyKeyAndDifferentDraftPayload_ThrowsMismatchException()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const string idempotencyKey = "idemp-diff-draft";

        await repo.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            PaymentHoldDurationMinutes = 15
        }, null, CancellationToken.None);

        await repo.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, "Same Note", idempotencyKey, CancellationToken.None);

        // Save a different draft
        await repo.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            PaymentHoldDurationMinutes = 25
        }, null, CancellationToken.None);

        // Attempt publish with same key and same note/mode, but different draft payload!
        await Assert.ThrowsAsync<IdempotencyPayloadMismatchException>(() =>
            repo.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, "Same Note", idempotencyKey, CancellationToken.None));
    }

    [Fact]
    public async Task Publish_WhenNoDraftExists_ThrowsNoDraftToPublishException()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await Assert.ThrowsAsync<NoDraftToPublishException>(() =>
            repo.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, "No draft", "key-none", CancellationToken.None));
    }
}
