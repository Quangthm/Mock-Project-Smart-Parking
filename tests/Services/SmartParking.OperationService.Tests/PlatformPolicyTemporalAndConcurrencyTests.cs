using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Policies;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;
using Xunit;

namespace SmartParking.OperationService.Tests;

/// <summary>
/// SPARK-370 Cluster 3: Temporal validity, composed invariants, and concurrency tests.
/// Verifies:
/// 1. Owner scheduling a site policy crossing a future scheduled platform revision with narrower bounds
///    is rejected (PolicyOutOfBoundsException).
/// 2. Admin platform activation that invalidates an active site policy's composed invariant (min > max)
///    is rejected with 409 Conflict (PolicyBoundsConflictException).
/// 3. Concurrent Admin activation and Owner site publish maintain consistency and conflict detection.
/// 4. Legacy DRIVER_REALLOCATION_REQUEST_LIMIT (value 2) on existing site policies resolves without conflict.
/// </summary>
public class PlatformPolicyTemporalAndConcurrencyTests
{
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static ReservationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ReservationDbContext(options);
    }

    private static OperationalPolicyRow PlatformRow(
        DateTimeOffset effectiveFrom,
        int version = 1,
        bool isActive = true,
        DateTimeOffset? effectiveUntil = null,
        string? overrideBounds = null,
        int? minReservationDurationMinutes = 15,
        int? maxReservationDurationHours = 24)
    {
        return new OperationalPolicyRow
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            SiteId = null,
            Version = version,
            IsActive = isActive,
            CreatedAt = effectiveFrom,
            UpdatedAt = effectiveFrom,
            EffectiveFrom = effectiveFrom,
            EffectiveUntil = effectiveUntil,
            PaymentHoldDurationMinutes = 15,
            MinReservationDurationMinutes = minReservationDurationMinutes,
            MaxReservationDurationHours = maxReservationDurationHours,
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
    }

    [Fact]
    public async Task OwnerPublish_CrossingFutureScheduledPlatformRevisionWithNarrowerBounds_ThrowsPolicyOutOfBounds()
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;

        // Platform Revision 1: currently active with wide bounds [15..90] minutes
        var platformV1 = PlatformRow(
            effectiveFrom: now.AddDays(-10),
            version: 1,
            isActive: true,
            effectiveUntil: now.AddDays(7),
            overrideBounds: "{\"RESERVATION_MIN_DURATION\":{\"Minimum\":15,\"Maximum\":90}}"
        );

        // Platform Revision 2: scheduled in 7 days with narrower bounds [15..30] minutes
        var platformV2 = PlatformRow(
            effectiveFrom: now.AddDays(7),
            version: 2,
            isActive: true,
            effectiveUntil: null,
            overrideBounds: "{\"RESERVATION_MIN_DURATION\":{\"Minimum\":15,\"Maximum\":30}}"
        );

        db.Set<OperationalPolicyRow>().AddRange(platformV1, platformV2);
        await db.SaveChangesAsync();

        var lotRepo = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        // Site owner creates a draft with MinReservationDuration = 45 (permitted by v1, but violates scheduled v2)
        await lotRepo.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            MinReservationDurationMinutes = 45
        }, null, CancellationToken.None);

        // Publishing site policy starting now with open-ended interval crosses into platform v2
        var ex = await Assert.ThrowsAsync<PolicyOutOfBoundsException>(() =>
            lotRepo.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, "open-ended site policy", null, CancellationToken.None));

        Assert.Equal("RESERVATION_MIN_DURATION", ex.ParameterName);
    }

    [Fact]
    public async Task AdminActivation_ComposedInvariantViolation_ThrowsPolicyBoundsConflictException()
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedTimeProvider(now);

        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Active site policy has maximum duration of 1 hour (60 minutes)
        var activeSite = new OperationalPolicyRow
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            Version = 1,
            IsActive = true,
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now.AddDays(-5),
            EffectiveFrom = now.AddDays(-5),
            EffectiveUntil = null,
            PaymentHoldDurationMinutes = 15,
            // The site inherits the platform minimum duration (null). The composed minimum
            // therefore comes from the platform revision below, while the site keeps a 1-hour
            // maximum override, producing an invalid composed policy (max < min).
            MinReservationDurationMinutes = null,
            MaxReservationDurationHours = 1, // 60 minutes
            MaxAdvanceBookingDays = 7,
            InterBookingBufferMinutes = 0,
            ReservationProtectionWindowHours = 4,
            AllocationLeadTimeMinutes = 60,
            FacilityFailureRefundLeadMinutes = 15,
            OccupancyValidityTimespanHours = 4,
            ArrivalLateToleranceMinutes = 15,
            ArrivalLateToleranceEnabled = true,
            CancellationTimezone = "Asia/Ho_Chi_Minh"
        };

        // Draft platform revision that Admin wants to activate has minimum duration = 90 minutes.
        // If activated, activeSite would compose: min (90) > max (60) which violates business invariants!
        var platformDraft = PlatformRow(
            effectiveFrom: now.AddDays(1),
            version: 2,
            isActive: false,
            minReservationDurationMinutes: 90,
            maxReservationDurationHours: 24,
            // The bounds still cover the site's 1-hour (60 min) maximum override, so the per-key
            // override-bounds loop passes and the composed-invariant check is exercised.
            overrideBounds: "{\"RESERVATION_MAX_DURATION\":{\"Minimum\":15,\"Maximum\":120}}"
        );

        db.Set<OperationalPolicyRow>().AddRange(activeSite, platformDraft);
        await db.SaveChangesAsync();

        var reservationRepo = new ReservationPolicyRepository(db, clock);

        var ex = await Assert.ThrowsAsync<PolicyBoundsConflictException>(() =>
            reservationRepo.ValidateIntersectingSiteCompatibilityAsync(
                platformDraft,
                now.AddDays(1),
                null,
                CancellationToken.None));

        Assert.Equal(siteId, ex.SiteId);
        Assert.Equal("RESERVATION_MAX_DURATION", ex.PolicyKey);
        Assert.Contains("composed maximum reservation duration", ex.Message);
    }

    [Fact]
    public async Task LegacyDriverReallocationRequestLimit_WithSiteValue2_DoesNotConflictOnPlatformActivation()
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedTimeProvider(now);

        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Site has established value 2 for DriverReallocationRequestLimit
        var activeSite = new OperationalPolicyRow
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            Version = 1,
            IsActive = true,
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now.AddDays(-5),
            EffectiveFrom = now.AddDays(-5),
            EffectiveUntil = null,
            DriverReallocationRequestLimit = 2,
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
            CancellationTimezone = "Asia/Ho_Chi_Minh"
        };

        // New platform revision has no bound for DRIVER_REALLOCATION_REQUEST_LIMIT
        var platformDraft = PlatformRow(
            effectiveFrom: now.AddDays(1),
            version: 2,
            isActive: false,
            overrideBounds: "{\"RESERVATION_MIN_DURATION\":{\"Minimum\":15,\"Maximum\":60}}"
        );

        db.Set<OperationalPolicyRow>().AddRange(activeSite, platformDraft);
        await db.SaveChangesAsync();

        var reservationRepo = new ReservationPolicyRepository(db, clock);

        // Validating compatibility should NOT throw for site with value 2
        var exception = await Record.ExceptionAsync(() =>
            reservationRepo.ValidateIntersectingSiteCompatibilityAsync(
                platformDraft,
                now.AddDays(1),
                null,
                CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Concurrency_ConcurrentAdminPlatformActivationAndOwnerPublish_DetectsConflictsSafely()
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedTimeProvider(now);

        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        // Base active platform revision
        var platformV1 = PlatformRow(
            effectiveFrom: now.AddDays(-10),
            version: 1,
            isActive: true,
            effectiveUntil: null,
            overrideBounds: "{\"RESERVATION_MIN_DURATION\":{\"Minimum\":15,\"Maximum\":120}}"
        );

        db.Set<OperationalPolicyRow>().Add(platformV1);
        await db.SaveChangesAsync();

        var lotRepo = new LotPolicyRepository(db);

        // Owner prepares a draft with MinReservationDuration = 90 minutes
        await lotRepo.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            MinReservationDurationMinutes = 90
        }, null, CancellationToken.None);

        // Platform admin concurrently introduces a tighter bound [15..60] minutes
        var tighterPlatform = PlatformRow(
            effectiveFrom: now.AddMinutes(5),
            version: 2,
            isActive: true, // Activated concurrently
            effectiveUntil: null,
            overrideBounds: "{\"RESERVATION_MIN_DURATION\":{\"Minimum\":15,\"Maximum\":60}}"
        );
        db.Set<OperationalPolicyRow>().Add(tighterPlatform);
        await db.SaveChangesAsync();

        // When Owner publish transaction executes against the updated database state,
        // it must observe the intersecting tighter platform revision and reject rather than committing invalid state.
        var ex = await Assert.ThrowsAsync<PolicyOutOfBoundsException>(() =>
            lotRepo.PublishDraftAsync(siteId, tenantId, actorId, "IMMEDIATE", null, "concurrent publish", null, CancellationToken.None));

        Assert.Equal("RESERVATION_MIN_DURATION", ex.ParameterName);
    }
}
