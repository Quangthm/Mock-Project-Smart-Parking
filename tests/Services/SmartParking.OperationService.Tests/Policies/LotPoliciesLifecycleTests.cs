using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Policies.Commands;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Ports;
using SmartParking.OperationService.Application.Policies.Queries;
using SmartParking.OperationService.Domain.Policies;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests.Policies;

public class LotPoliciesLifecycleTests
{
    private sealed class FakeOwnershipValidator(bool allow = true, Guid? tenantId = null) : ISiteOwnershipValidator
    {
        public Task<Guid> ValidateOwnershipAsync(Guid? actorId, Guid siteId, CancellationToken ct)
        {
            if (!allow) throw new CrossLotAccessException();
            return Task.FromResult(tenantId ?? Guid.Parse("11111111-2222-3333-4444-555555555555"));
        }
    }

    private static ReservationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new ReservationDbContext(options);

        // Seed initial platform defaults
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
            InterBookingBufferMinutes = 0,
            OverrideBounds = "{\"PAYMENT_HOLD_DURATION\":{\"Minimum\":5,\"Maximum\":30}}"
        });
        db.SaveChanges();

        return db;
    }

    [Fact]
    public async Task GetCatalogue_ReturnsEffectiveValuesWithPlatformFallback()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var validator = new FakeOwnershipValidator(allow: true);
        var handler = new GetLotPolicyCatalogueQueryHandler(repo, validator);

        var siteId = Guid.NewGuid();
        var result = await handler.Handle(new GetLotPolicyCatalogueQuery(siteId, Guid.NewGuid()), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(siteId, result.SiteId);
        Assert.False(result.HasDraft);
        Assert.NotEmpty(result.Policies);

        var holdPolicy = result.Policies.First(p => p.Key == "PAYMENT_HOLD_DURATION");
        Assert.Equal(15, holdPolicy.AdminDefault);
        Assert.Equal(15, holdPolicy.EffectiveValue);
        Assert.Equal("ADMIN_DEFAULT", holdPolicy.Source);
    }

    [Fact]
    public async Task FullLifecycle_SaveDraft_Publish_VerifyRevisionAndSnapshot()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var validator = new FakeOwnershipValidator(allow: true);
        var siteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        // 1. Save Draft
        var saveHandler = new SaveLotPolicyDraftCommandHandler(repo, validator);
        var saveResult = await saveHandler.Handle(new SaveLotPolicyDraftCommand(
            siteId,
            actorId,
            new SavePolicyDraftRequest
            {
                PaymentHoldDurationMinutes = 10,
                ArrivalLateToleranceMinutes = 20,
                MinReservationDurationMinutes = 30
            }
        ), CancellationToken.None);

        Assert.Equal("DRAFT", saveResult.Status);
        Assert.Equal(siteId, saveResult.SiteId);

        // 2. Query Draft
        var getDraftHandler = new GetLotPolicyDraftQueryHandler(repo, validator);
        var draft = await getDraftHandler.Handle(new GetLotPolicyDraftQuery(siteId, actorId), CancellationToken.None);
        Assert.NotNull(draft);
        Assert.Equal(10, draft.Policies.PaymentHoldDurationMinutes);
        Assert.Equal(20, draft.Policies.ArrivalLateToleranceMinutes);

        // 3. Publish Draft
        var publishHandler = new PublishLotPolicyCommandHandler(repo, validator);
        var publishResult = await publishHandler.Handle(new PublishLotPolicyCommand(
            siteId,
            actorId,
            "idempotency-key-1",
            new PublishPolicyRequest { Mode = "IMMEDIATE" }
        ), CancellationToken.None);

        Assert.Equal("ACTIVE", publishResult.Status);
        Assert.Equal(1, publishResult.NewRevision);

        // 4. Catalogue now reflects OWNER_OVERRIDE
        var catalogueHandler = new GetLotPolicyCatalogueQueryHandler(repo, validator);
        var catalogue = await catalogueHandler.Handle(new GetLotPolicyCatalogueQuery(siteId, actorId), CancellationToken.None);
        Assert.False(catalogue.HasDraft);
        var hold = catalogue.Policies.First(p => p.Key == "PAYMENT_HOLD_DURATION");
        Assert.Equal(10, hold.EffectiveValue);
        Assert.Equal("OWNER_OVERRIDE", hold.Source);

        // 5. Query Revisions
        var revisionsHandler = new GetLotPolicyRevisionsQueryHandler(repo, validator);
        var revisions = await revisionsHandler.Handle(new GetLotPolicyRevisionsQuery(siteId, actorId), CancellationToken.None);
        Assert.Single(revisions.Items);
        Assert.Equal(1, revisions.Items[0].RevisionNumber);
        Assert.Equal("ACTIVE", revisions.Items[0].Status);

        // 6. Query Snapshot
        var snapshotHandler = new GetLotPolicyRevisionSnapshotQueryHandler(repo, validator);
        var snapshot = await snapshotHandler.Handle(new GetLotPolicyRevisionSnapshotQuery(siteId, 1, actorId), CancellationToken.None);
        Assert.Equal(1, snapshot.RevisionNumber);
        Assert.Equal(10, snapshot.Policies["paymentHoldDurationMinutes"]);
    }

    [Fact]
    public async Task Publish_WhenNoDraftExists_ThrowsNoDraftToPublishException()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var validator = new FakeOwnershipValidator(allow: true);
        var publishHandler = new PublishLotPolicyCommandHandler(repo, validator);

        await Assert.ThrowsAsync<NoDraftToPublishException>(() =>
            publishHandler.Handle(new PublishLotPolicyCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "key",
                new PublishPolicyRequest { Mode = "IMMEDIATE" }
            ), CancellationToken.None));
    }

    [Fact]
    public async Task Access_WhenOwnerDoesNotOwnLot_ThrowsCrossLotAccessException()
    {
        using var db = CreateInMemoryDb();
        var repo = new LotPolicyRepository(db);
        var validator = new FakeOwnershipValidator(allow: false);
        var catalogueHandler = new GetLotPolicyCatalogueQueryHandler(repo, validator);

        await Assert.ThrowsAsync<CrossLotAccessException>(() =>
            catalogueHandler.Handle(new GetLotPolicyCatalogueQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }
}
