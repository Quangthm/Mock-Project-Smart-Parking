using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Policies.Commands;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Validators;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Policies;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests;

/// <summary>
/// SPARK-340 Phase 4 preparation: regression coverage for the platform policy
/// validation bounds, minute conversion safety, revision status classification,
/// owner publish bounds and the owner catalogue display-unit bounds.
/// </summary>
public class PlatformPoliciesTests
{
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class DraftInput : IPolicyDraftInput
    {
        public int MinReservationDurationMinutes { get; set; } = 15;
        public int MaxReservationDurationHours { get; set; } = 24;
        public int PaymentHoldDurationMinutes { get; set; } = 15;
        public int MaxAdvanceBookingDays { get; set; } = 7;
        public int InterBookingBufferMinutes { get; set; }
        public int ReservationProtectionWindowHours { get; set; } = 4;
        public int AllocationLeadTimeMinutes { get; set; } = 60;
        public int FacilityFailureRefundLeadMinutes { get; set; } = 15;
        public int OccupancyValidityTimespanHours { get; set; } = 4;
        public int ArrivalLateToleranceMinutes { get; set; } = 15;
        public int RapidCancellationCount { get; set; } = 2;
        public int RapidCancellationWindowMinutes { get; set; } = 15;
        public int RapidCancellationRestrictionMinutes { get; set; } = 180;
        public int DailyCancellationLimit { get; set; } = 5;
    }

    private sealed class DraftInputValidator : AbstractValidator<DraftInput>
    {
        public DraftInputValidator() => this.ApplyPolicyDraftRules();
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
        int? maxReservationDurationHours = 24,
        int? maxAdvanceBookingDays = 7,
        int? reservationProtectionWindowHours = 4,
        int? occupancyValidityTimespanHours = 4,
        int? minReservationDurationMinutes = 15)
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
            MaxAdvanceBookingDays = maxAdvanceBookingDays,
            InterBookingBufferMinutes = 0,
            ReservationProtectionWindowHours = reservationProtectionWindowHours,
            AllocationLeadTimeMinutes = 60,
            FacilityFailureRefundLeadMinutes = 15,
            OccupancyValidityTimespanHours = occupancyValidityTimespanHours,
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

    private static OperationalPolicyRow SiteRow(
        Guid tenantId,
        Guid siteId,
        DateTimeOffset effectiveFrom,
        int version = 1,
        bool isActive = true,
        DateTimeOffset? effectiveUntil = null,
        int? paymentHoldDurationMinutes = null)
    {
        return new OperationalPolicyRow
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            Version = version,
            IsActive = isActive,
            CreatedAt = effectiveFrom,
            UpdatedAt = effectiveFrom,
            EffectiveFrom = effectiveFrom,
            EffectiveUntil = effectiveUntil,
            PaymentHoldDurationMinutes = paymentHoldDurationMinutes
        };
    }

    // ---------------------------------------------------------------------
    // Finding 1: PolicyDraftValidationRules upper bounds.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("RESERVATION_MAX_DURATION")]
    [InlineData("MAX_ADVANCE_BOOKING")]
    public async Task MinuteBoundsAtIntMax_MustContainARepresentableDisplayValue(string key)
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var repository = new ReservationPolicyRepository(db, TimeProvider.System);
        var bounds = new Dictionary<string, PolicyBoundDto> { [key] = new(int.MaxValue, int.MaxValue) };
        var handler = new CreatePolicyDraftHandler(repository, TimeProvider.System);
        var command = new CreatePolicyDraftCommand(null, null, 15, 15, 24, 7, 0, 4, 60, 15, 4, 15, 2, 15, 180, 5,
            null, "Asia/Ho_Chi_Minh", bounds);
        await Assert.ThrowsAsync<InvalidPolicyBoundsException>(() => handler.Handle(command, CancellationToken.None));

        var platform = PlatformRow(now.AddHours(-1), overrideBounds: System.Text.Json.JsonSerializer.Serialize(bounds));
        db.Add(platform);
        await db.SaveChangesAsync();
        var catalogue = await new LotPolicyRepository(db).GetCatalogueAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Null(catalogue.Policies.Single(p => p.Key == key).AdminBounds);
    }

    [Fact]
    public void DraftValidator_RejectsEachUpperBoundViolation_WithExpectedPropertyName()
    {
        var validator = new DraftInputValidator();
        var cases = new (Action<DraftInput> Mutate, string Property)[]
        {
            (draft => draft.MaxAdvanceBookingDays = int.MaxValue / 1440 + 1, "MaxAdvanceBookingDays"),
            (draft => draft.MaxReservationDurationHours = int.MaxValue / 60 + 1, "MaxReservationDurationHours"),
            (draft => draft.ReservationProtectionWindowHours = int.MaxValue / 60 + 1, "ReservationProtectionWindowHours"),
            (draft => draft.OccupancyValidityTimespanHours = int.MaxValue / 60 + 1, "OccupancyValidityTimespanHours")
        };

        foreach (var (mutate, property) in cases)
        {
            var draft = new DraftInput();
            mutate(draft);

            var result = validator.Validate(draft);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == property);
        }
    }

    [Fact]
    public void DraftValidator_AcceptsActualTechnicalCeilingValues()
    {
        // The shared rules cap hour and day values so the converted minute value cannot overflow a
        // signed 32-bit integer; these are representability limits, not product bounds.
        var draft = new DraftInput
        {
            MaxAdvanceBookingDays = int.MaxValue / 1440,
            MaxReservationDurationHours = int.MaxValue / 60,
            ReservationProtectionWindowHours = int.MaxValue / 60,
            OccupancyValidityTimespanHours = int.MaxValue / 60
        };

        var result = new DraftInputValidator().Validate(draft);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void CreatePolicyDraftCommandValidator_UsesSharedUpperBounds()
    {
        var validator = new CreatePolicyDraftCommandValidator();

        var valid = validator.Validate(new CreatePolicyDraftCommand(
            null, null, 15, 15, 24, 7, 0, 4, 60, 15, 4, 15, 2, 15, 180, 5,
            true, "Asia/Ho_Chi_Minh", null));
        Assert.True(valid.IsValid);

        var invalid = validator.Validate(new CreatePolicyDraftCommand(
            null, null, 15, 15, int.MaxValue / 60 + 1, int.MaxValue / 1440 + 1, 0, int.MaxValue / 60 + 1,
            60, 15, int.MaxValue / 60 + 1, 15, 2, 15, 180, 5,
            true, "Asia/Ho_Chi_Minh", null));

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Errors, error => error.PropertyName == "MaxReservationDurationHours");
        Assert.Contains(invalid.Errors, error => error.PropertyName == "MaxAdvanceBookingDays");
        Assert.Contains(invalid.Errors, error => error.PropertyName == "ReservationProtectionWindowHours");
        Assert.Contains(invalid.Errors, error => error.PropertyName == "OccupancyValidityTimespanHours");
    }

    // ---------------------------------------------------------------------
    // Finding 2: hour/day -> minute conversion must not overflow.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ToRevision_ConvertsLargeButValidHourAndDayValues_AsIntMinutesWithoutOverflow()
    {
        using var db = CreateInMemoryDb();
        var now = new DateTimeOffset(2026, 11, 13, 5, 0, 0, TimeSpan.Zero);
        db.Set<OperationalPolicyRow>().Add(PlatformRow(
            now.AddDays(-1),
            maxReservationDurationHours: 8760,
            maxAdvanceBookingDays: 365,
            reservationProtectionWindowHours: 8760,
            occupancyValidityTimespanHours: 8760));
        await db.SaveChangesAsync();

        var repository = new ReservationPolicyRepository(db, new FixedTimeProvider(now));

        var revision = await repository.GetActivePlatformRevisionAsync(now, CancellationToken.None);

        Assert.NotNull(revision);
        Assert.Equal(8760 * 60, revision!.Values["RESERVATION_MAX_DURATION"]);
        Assert.Equal(365 * 1440, revision.Values["MAX_ADVANCE_BOOKING"]);
        Assert.Equal(8760 * 60, revision.Values["RESERVATION_PROTECTION_WINDOW"]);
        Assert.Equal(8760 * 60, revision.Values["OCCUPANCY_VALIDITY_TIMESPAN"]);
        Assert.IsType<int>(revision.Values["RESERVATION_MAX_DURATION"]);
        Assert.IsType<int>(revision.Values["MAX_ADVANCE_BOOKING"]);
    }

    [Fact]
    public async Task ToRevision_WhenRawHourValueExceedsIntMinuteRange_ThrowsDomainExceptionNotOverflow()
    {
        using var db = CreateInMemoryDb();
        var now = new DateTimeOffset(2026, 11, 13, 5, 0, 0, TimeSpan.Zero);
        db.Set<OperationalPolicyRow>().Add(PlatformRow(
            now.AddDays(-1),
            maxReservationDurationHours: int.MaxValue / 2));
        await db.SaveChangesAsync();

        var repository = new ReservationPolicyRepository(db, new FixedTimeProvider(now));

        await Assert.ThrowsAsync<ReservationPolicyUnavailableException>(() =>
            repository.GetActivePlatformRevisionAsync(now, CancellationToken.None));
    }

    // ---------------------------------------------------------------------
    // Finding 3: revision status classification.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task RevisionStatus_ClassifiesSupersededDraftScheduledAndCurrent_FromFixedClock()
    {
        using var db = CreateInMemoryDb();
        var now = new DateTimeOffset(2026, 11, 13, 5, 0, 0, TimeSpan.Zero);

        var expiredActive = PlatformRow(now.AddDays(-3), version: 1, isActive: true, effectiveUntil: now.AddHours(-1));
        var expiredInactive = PlatformRow(now.AddDays(-2), version: 2, isActive: false, effectiveUntil: now.AddMinutes(-5));
        var draft = PlatformRow(now.AddDays(-1), version: 3, isActive: false);
        var scheduled = PlatformRow(now.AddDays(2), version: 4, isActive: true);
        var current = PlatformRow(now.AddDays(-1), version: 5, isActive: true);

        db.Set<OperationalPolicyRow>().AddRange(expiredActive, expiredInactive, draft, scheduled, current);
        await db.SaveChangesAsync();

        var repository = new ReservationPolicyRepository(db, new FixedTimeProvider(now));

        var all = await repository.GetRevisionsAsync("", "PLATFORM", null, null, 1, 50, CancellationToken.None);
        var byId = new Dictionary<Guid, string>();
        foreach (var row in new[] { expiredActive, expiredInactive, draft, scheduled, current })
        {
            var item = await repository.GetRevisionByIdAsync(row.Id, CancellationToken.None);
            Assert.NotNull(item);
            byId[row.Id] = item!.Status;
        }

        Assert.Equal("SUPERSEDED", byId[expiredActive.Id]);
        Assert.Equal("SUPERSEDED", byId[expiredInactive.Id]);
        Assert.Equal("DRAFT", byId[draft.Id]);
        Assert.Equal("SCHEDULED", byId[scheduled.Id]);
        Assert.Equal("CURRENT", byId[current.Id]);

        Assert.Equal(5, all.TotalCount);
        Assert.Equal("SUPERSEDED", all.Items.Single(item => item.Id == expiredActive.Id).Status);
        Assert.Equal("SUPERSEDED", all.Items.Single(item => item.Id == expiredInactive.Id).Status);
        Assert.Equal("DRAFT", all.Items.Single(item => item.Id == draft.Id).Status);
        Assert.Equal("SCHEDULED", all.Items.Single(item => item.Id == scheduled.Id).Status);
        Assert.Equal("CURRENT", all.Items.Single(item => item.Id == current.Id).Status);
    }

    // ---------------------------------------------------------------------
    // Finding 4: owner publish bounds enforcement.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task OwnerPublish_WithDraftValueInsideApprovedBound_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var platform = PlatformRow(
            DateTimeOffset.UtcNow.AddDays(-1),
            overrideBounds: "{\"RESERVATION_PROTECTION_WINDOW\":{\"Minimum\":120,\"Maximum\":480}}",
            reservationProtectionWindowHours: 4);
        db.Set<OperationalPolicyRow>().Add(platform);
        await db.SaveChangesAsync();

        var repository = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await repository.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            ReservationProtectionWindowHours = 6
        }, null, CancellationToken.None);

        var published = await repository.PublishDraftAsync(
            siteId, tenantId, actorId, "IMMEDIATE", null, "inside bound", null, CancellationToken.None);

        Assert.Equal(1, published.NewRevision);
        Assert.Equal("ACTIVE", published.Status);
    }

    [Fact]
    public async Task OwnerPublish_WithDraftValueOutsideApprovedBound_ThrowsPolicyOutOfBounds()
    {
        using var db = CreateInMemoryDb();
        var platform = PlatformRow(
            DateTimeOffset.UtcNow.AddDays(-1),
            overrideBounds: "{\"RESERVATION_PROTECTION_WINDOW\":{\"Minimum\":120,\"Maximum\":480}}",
            reservationProtectionWindowHours: 4);
        db.Set<OperationalPolicyRow>().Add(platform);
        await db.SaveChangesAsync();

        var repository = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await repository.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            ReservationProtectionWindowHours = 12
        }, null, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<PolicyOutOfBoundsException>(() =>
            repository.PublishDraftAsync(
                siteId, tenantId, actorId, "IMMEDIATE", null, "outside bound", null, CancellationToken.None));

        Assert.Equal("RESERVATION_PROTECTION_WINDOW", exception.ParameterName);
    }

    [Fact]
    public async Task OwnerPublish_WhenKeyHasNoApprovedBoundAndDiffersFromPlatform_ThrowsPolicyOutOfBounds()
    {
        using var db = CreateInMemoryDb();
        var platform = PlatformRow(
            DateTimeOffset.UtcNow.AddDays(-1),
            overrideBounds: "{\"RESERVATION_PROTECTION_WINDOW\":{\"Minimum\":120,\"Maximum\":480}}",
            maxAdvanceBookingDays: 7);
        db.Set<OperationalPolicyRow>().Add(platform);
        await db.SaveChangesAsync();

        var repository = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await repository.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            MaxAdvanceBookingDays = 10
        }, null, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<PolicyOutOfBoundsException>(() =>
            repository.PublishDraftAsync(
                siteId, tenantId, actorId, "IMMEDIATE", null, "no bound", null, CancellationToken.None));

        Assert.Equal("MAX_ADVANCE_BOOKING", exception.ParameterName);
    }

    // ---------------------------------------------------------------------
    // Finding 5: owner catalogue bounds and currently effective revision.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task OwnerCatalogue_ReturnsConvertedDisplayUnitBounds_AndNoFallbackForUnboundedKeys()
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var current = PlatformRow(
            now.AddDays(-1),
            version: 1,
            overrideBounds: "{\"RESERVATION_MAX_DURATION\":{\"Minimum\":120,\"Maximum\":600}}",
            maxReservationDurationHours: 24);
        var future = PlatformRow(
            now.AddDays(2),
            version: 2,
            overrideBounds: "{\"RESERVATION_MAX_DURATION\":{\"Minimum\":60,\"Maximum\":1200}}",
            maxReservationDurationHours: 48);
        db.Set<OperationalPolicyRow>().AddRange(current, future);
        await db.SaveChangesAsync();

        var repository = new LotPolicyRepository(db);
        var catalogue = await repository.GetCatalogueAsync(Guid.NewGuid(), CancellationToken.None);

        var maxDuration = catalogue.Policies.Single(item => item.Key == "RESERVATION_MAX_DURATION");
        Assert.Equal("hours", maxDuration.Unit);
        Assert.Equal(24, maxDuration.AdminDefault);
        Assert.NotNull(maxDuration.AdminBounds);
        Assert.Equal(2, maxDuration.AdminBounds!.Min);
        Assert.Equal(10, maxDuration.AdminBounds.Max);
        Assert.True(maxDuration.IsConfigurable);

        // ALLOCATION_LEAD_TIME is absent from override_bounds: no fabricated fallback.
        var allocationLeadTime = catalogue.Policies.Single(item => item.Key == "ALLOCATION_LEAD_TIME");
        Assert.Null(allocationLeadTime.AdminBounds);
        Assert.False(allocationLeadTime.IsConfigurable);

        var paymentHold = catalogue.Policies.Single(item => item.Key == "PAYMENT_HOLD_DURATION");
        Assert.Null(paymentHold.AdminBounds);
        Assert.False(paymentHold.IsConfigurable);
    }

    // ---------------------------------------------------------------------
    // Round 2 hardening: scheduled publish uses the platform effective at effectiveFrom.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task OwnerPublish_Scheduled_UsesPlatformRevisionEffectiveAtScheduledTime()
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var scheduledAt = now.AddDays(3);

        // Revision 1 is effective now but only permits 120..240 minutes for the protection window.
        var current = PlatformRow(now.AddDays(-10), version: 1, effectiveUntil: now.AddDays(1),
            overrideBounds: "{\"RESERVATION_PROTECTION_WINDOW\":{\"Minimum\":120,\"Maximum\":240}}");
        // Revision 2 takes effect before the scheduled publish and permits 240..720 minutes.
        var upcoming = PlatformRow(now.AddDays(2), version: 2,
            overrideBounds: "{\"RESERVATION_PROTECTION_WINDOW\":{\"Minimum\":240,\"Maximum\":720}}");
        db.Set<OperationalPolicyRow>().AddRange(current, upcoming);
        await db.SaveChangesAsync();

        var repository = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        // 480 minutes is rejected by revision 1 but accepted by revision 2.
        await repository.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            ReservationProtectionWindowHours = 8
        }, scheduledAt, CancellationToken.None);

        var published = await repository.PublishDraftAsync(
            siteId, tenantId, actorId, "SCHEDULED", scheduledAt, "scheduled", null, CancellationToken.None);

        Assert.Equal("SCHEDULED", published.Status);
        Assert.Equal(scheduledAt, published.EffectiveFrom);
    }

    [Fact]
    public async Task OwnerPublish_Scheduled_WhenNoPlatformEffectiveAtScheduledTime_FailsClosed()
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var scheduledAt = now.AddDays(3);

        // Platform is effective now but expires before the scheduled activation time.
        db.Set<OperationalPolicyRow>().Add(PlatformRow(now.AddDays(-10), version: 1,
            effectiveUntil: now.AddDays(1)));
        await db.SaveChangesAsync();

        var repository = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await repository.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            PaymentHoldDurationMinutes = 15
        }, scheduledAt, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ReservationPolicyUnavailableException>(() =>
            repository.PublishDraftAsync(
                siteId, tenantId, actorId, "SCHEDULED", scheduledAt, "gap", null, CancellationToken.None));

        Assert.Contains("Platform policy is required", exception.Message);
        Assert.Empty(await db.Set<OperationalPolicyRow>().Where(x => x.SiteId == siteId).ToListAsync());
        Assert.NotNull(await repository.GetDraftAsync(siteId, CancellationToken.None));
    }

    // ---------------------------------------------------------------------
    // Round 2 hardening: platform activation must stay compatible with site policies.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task PlatformActivation_WhenIntersectingSiteOverrideOutsideNewBounds_ThrowsConflictNamingSiteAndKey()
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;
        var activation = now.AddHours(1);

        var target = PlatformRow(now, version: 2, isActive: false,
            overrideBounds: "{\"PAYMENT_HOLD_DURATION\":{\"Minimum\":5,\"Maximum\":20}}");
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var site = SiteRow(tenantId, siteId, now.AddDays(-1), version: 2,
            effectiveUntil: now.AddDays(10), paymentHoldDurationMinutes: 45);

        db.Set<OperationalPolicyRow>().AddRange(target, site);
        await db.SaveChangesAsync();

        var repository = new ReservationPolicyRepository(db, new FixedTimeProvider(now));

        var exception = await Assert.ThrowsAsync<PolicyBoundsConflictException>(() =>
            repository.ValidateIntersectingSiteCompatibilityAsync(target, activation, null, CancellationToken.None));

        Assert.Equal(siteId, exception.SiteId);
        Assert.Equal("PAYMENT_HOLD_DURATION", exception.PolicyKey);
        Assert.Contains(siteId.ToString(), exception.Message);
        Assert.Contains("PAYMENT_HOLD_DURATION", exception.Message);
    }

    // ---------------------------------------------------------------------
    // Round 2 hardening: composed post-inheritance invariants before publish.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task OwnerPublish_WhenComposedMaximumIsBelowInheritedMinimum_IsRejectedBeforeWrite()
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;

        // Platform minimum is 120 minutes, while the approved max-duration bound still allows 60.
        db.Set<OperationalPolicyRow>().Add(PlatformRow(now.AddDays(-1),
            minReservationDurationMinutes: 120,
            overrideBounds: "{\"RESERVATION_MAX_DURATION\":{\"Minimum\":60,\"Maximum\":600}}"));
        await db.SaveChangesAsync();

        var repository = new LotPolicyRepository(db);
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await repository.SaveDraftAsync(siteId, tenantId, actorId, new PolicyDraftConfig
        {
            MaxReservationDurationHours = 1 // 60 minutes, below the inherited 120-minute minimum
        }, null, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<PolicyOutOfBoundsException>(() =>
            repository.PublishDraftAsync(
                siteId, tenantId, actorId, "IMMEDIATE", null, "invalid composite", null, CancellationToken.None));

        Assert.Equal("RESERVATION_MAX_DURATION", exception.ParameterName);
        Assert.Empty(await db.Set<OperationalPolicyRow>().Where(x => x.SiteId == siteId).ToListAsync());
        Assert.NotNull(await repository.GetDraftAsync(siteId, CancellationToken.None));
    }

    // ---------------------------------------------------------------------
    // Round 2 hardening: owner validator accepts Admin-approved wide ranges.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task OwnerDraftWideValueWithinAdminBounds_IsAcceptedByValidatorAndPublishes()
    {
        using var db = CreateInMemoryDb();
        var now = DateTimeOffset.UtcNow;

        db.Set<OperationalPolicyRow>().Add(PlatformRow(now.AddDays(-1),
            minReservationDurationMinutes: 15,
            overrideBounds: "{\"RESERVATION_MIN_DURATION\":{\"Minimum\":15,\"Maximum\":90}}"));
        await db.SaveChangesAsync();

        var siteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var command = new SaveLotPolicyDraftCommand(
            SiteId: siteId,
            ActorId: actorId,
            Request: new SavePolicyDraftRequest { MinReservationDurationMinutes = 75 });

        var validation = new SaveLotPolicyDraftCommandValidator().Validate(command);
        Assert.True(validation.IsValid,
            string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));

        var repository = new LotPolicyRepository(db);
        var tenantId = Guid.NewGuid();
        await repository.SaveDraftAsync(siteId, tenantId, actorId,
            command.Request.ToDraftConfig(), null, CancellationToken.None);

        var published = await repository.PublishDraftAsync(
            siteId, tenantId, actorId, "IMMEDIATE", null, "wide range", null, CancellationToken.None);

        Assert.Equal("ACTIVE", published.Status);
    }

    // ---------------------------------------------------------------------
    // Round 2 hardening: platform catalogue never fabricates an omitted bound.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task PlatformCatalogue_WhenRevisionOmitsOverrideBound_ReturnsNullInsteadOfMetadataDefault()
    {
        using var db = CreateInMemoryDb();
        var now = new DateTimeOffset(2026, 11, 13, 5, 0, 0, TimeSpan.Zero);
        db.Set<OperationalPolicyRow>().Add(PlatformRow(now.AddDays(-1),
            overrideBounds: "{\"RESERVATION_MAX_DURATION\":{\"Minimum\":120,\"Maximum\":600}}"));
        await db.SaveChangesAsync();

        var repository = new ReservationPolicyRepository(db, new FixedTimeProvider(now));
        var catalogue = await repository.GetCatalogueAsync(CancellationToken.None);

        // ARRIVAL_LATE_TOLERANCE has a metadata default bound (15..30), but revision 1 explicitly
        // omitted it, so it must surface as null rather than a fabricated bound.
        var tolerance = catalogue.Policies.Single(item => item.Key == "ARRIVAL_LATE_TOLERANCE");
        Assert.True(tolerance.IsOverridableByOwner);
        Assert.Null(tolerance.Bound);

        var maxDuration = catalogue.Policies.Single(item => item.Key == "RESERVATION_MAX_DURATION");
        Assert.NotNull(maxDuration.Bound);
        Assert.Equal(120, maxDuration.Bound!.Minimum);
        Assert.Equal(600, maxDuration.Bound.Maximum);
    }

    // ---------------------------------------------------------------------
    // Round 2 hardening: cancelled SITE revisions and DRAFT filtering.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SiteRevision_OwnerCancelledRow_ClassifiesSuperseded_AndDraftFilterExcludesSiteRows()
    {
        using var db = CreateInMemoryDb();
        var now = new DateTimeOffset(2026, 11, 13, 5, 0, 0, TimeSpan.Zero);
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        // Owner cancellation flips is_active only; effective_until stays null.
        var cancelledSite = SiteRow(tenantId, siteId, now.AddDays(1), version: 2,
            isActive: false, effectiveUntil: null);
        var platformDraft = PlatformRow(now.AddDays(-1), version: 3, isActive: false);

        db.Set<OperationalPolicyRow>().AddRange(cancelledSite, platformDraft);
        await db.SaveChangesAsync();

        var repository = new ReservationPolicyRepository(db, new FixedTimeProvider(now));

        var item = await repository.GetRevisionByIdAsync(cancelledSite.Id, CancellationToken.None);
        Assert.NotNull(item);
        Assert.Equal("SUPERSEDED", item!.Status);
        Assert.NotEqual("DRAFT", item.Status);

        var drafts = await repository.GetRevisionsAsync("DRAFT", "", null, null, 1, 50, CancellationToken.None);
        Assert.Equal(1, drafts.TotalCount);
        Assert.Equal(platformDraft.Id, drafts.Items.Single().Id);

        var siteDrafts = await repository.GetRevisionsAsync("DRAFT", "SITE", tenantId, siteId, 1, 50,
            CancellationToken.None);
        Assert.Equal(0, siteDrafts.TotalCount);

        var superseded = await repository.GetRevisionsAsync("SUPERSEDED", "SITE", tenantId, siteId, 1, 50,
            CancellationToken.None);
        Assert.Equal(1, superseded.TotalCount);
        Assert.Equal("SUPERSEDED", superseded.Items.Single().Status);
    }
}