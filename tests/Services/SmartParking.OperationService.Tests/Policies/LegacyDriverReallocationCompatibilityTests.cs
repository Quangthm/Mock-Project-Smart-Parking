using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Domain.Policies;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;
using Xunit;

namespace SmartParking.OperationService.Tests.Policies;

/// <summary>
/// SPARK-370 P2 regression coverage: the historical DRIVER_REALLOCATION_REQUEST_LIMIT value 2
/// must be accepted consistently by the catalogue, Owner republish and Admin activation, while
/// values outside the established legacy range stay rejected.
/// </summary>
public class LegacyDriverReallocationCompatibilityTests
{
    private static ReservationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ReservationDbContext(options);
    }

    private static OperationalPolicyRow ActivePlatform(DateTimeOffset now, string? overrideBounds) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = null,
        SiteId = null,
        Version = 1,
        IsActive = true,
        CreatedAt = now.AddDays(-1),
        UpdatedAt = now.AddDays(-1),
        EffectiveFrom = now.AddDays(-1),
        EffectiveUntil = null,
        PaymentHoldDurationMinutes = 15,
        MinReservationDurationMinutes = 15,
        MaxReservationDurationHours = 24,
        MaxAdvanceBookingDays = 7,
        InterBookingBufferMinutes = 0,
        ReservationProtectionWindowHours = 4,
        AllocationLeadTimeMinutes = 60,
        FacilityFailureRefundLeadMinutes = 15,
        OccupancyValidityTimespanHours = 4,
        ArrivalLateToleranceMinutes = 15,
        ArrivalLateToleranceEnabled = true,
        RapidCancellationCount = 2,
        RapidCancellationWindowMinutes = 15,
        RapidCancellationRestrictionMinutes = 180,
        DailyCancellationLimit = 5,
        CancellationTimezone = "Asia/Ho_Chi_Minh",
        OverrideBounds = overrideBounds
    };

    private static OperationalPolicyRow PublishedSite(Guid siteId, Guid tenantId, DateTimeOffset now, int value) => new()
    {
        Id = Guid.NewGuid(),
        SiteId = siteId,
        TenantId = tenantId,
        Version = 1,
        IsActive = true,
        CreatedAt = now,
        UpdatedAt = now,
        EffectiveFrom = now,
        ArrivalLateToleranceEnabled = true,
        DriverReallocationRequestLimit = value
    };

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task OwnerRepublish_PreservesUnchangedPublishedLimit(int value)
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        db.AddRange(ActivePlatform(now, null), PublishedSite(siteId, tenantId, now.AddHours(-1), value));
        await db.SaveChangesAsync();
        var repository = new LotPolicyRepository(db);

        await repository.SaveDraftAsync(siteId, tenantId, actorId,
            new PolicyDraftConfig { DriverReallocationRequestLimit = value }, null, CancellationToken.None);
        var published = await repository.PublishDraftAsync(siteId, tenantId, actorId,
            "IMMEDIATE", null, null, null, CancellationToken.None);

        Assert.Equal(2, published.NewRevision);
        var catalogue = await repository.GetCatalogueAsync(siteId, CancellationToken.None);
        var item = catalogue.Policies.Single(p => p.Key == PolicyCompatibility.DriverReallocationRequestLimitKey);
        Assert.Equal(value, Convert.ToInt32(item.EffectiveValue));
        Assert.False(item.IsConfigurable);
        Assert.Null(item.AdminBounds);
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData(null, 5)]
    [InlineData(2, 5)]
    [InlineData(7, 7)]
    public async Task OwnerPublish_WithoutApprovedBound_RejectsNewOrChangedLimit(int? previous, int requested)
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        db.Add(ActivePlatform(now, null));
        if (previous.HasValue) db.Add(PublishedSite(siteId, tenantId, now.AddHours(-1), previous.Value));
        await db.SaveChangesAsync();
        var repository = new LotPolicyRepository(db);

        await repository.SaveDraftAsync(siteId, tenantId, actorId,
            new PolicyDraftConfig { DriverReallocationRequestLimit = requested }, null, CancellationToken.None);
        var exception = await Assert.ThrowsAsync<PolicyOutOfBoundsException>(() =>
            repository.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, null, null, CancellationToken.None));

        Assert.Equal(PolicyCompatibility.DriverReallocationRequestLimitKey, exception.ParameterName);
        Assert.Equal(previous.HasValue ? 1 : 0, await db.Set<OperationalPolicyRow>().CountAsync(x => x.SiteId == siteId));
    }
}
