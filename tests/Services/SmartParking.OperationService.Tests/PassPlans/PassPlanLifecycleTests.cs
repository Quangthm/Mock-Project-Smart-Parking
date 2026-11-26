using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Application.PassPlans.Commands;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Options;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Application.PassPlans.Queries;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.PassPlans;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests.PassPlans;

public sealed class PassPlanLifecycleTests
{
    private static readonly Guid OwnerUserId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000001");
    private static readonly Guid SiteId = Guid.Parse("cccccccc-0000-4000-8000-000000000001");
    private static readonly Guid ZoneAId = Guid.Parse("dddddddd-0000-4000-8000-000000000001");
    private static readonly Guid ForeignZoneId = Guid.Parse("dddddddd-0000-4000-8000-000000000099");

    private static (ServiceProvider Provider, ReservationDbContext Db) BuildProvider(
        Func<Guid, Guid?, CancellationToken, Task<IReadOnlyList<PaymentPassFinancialTermDto>>>? paymentTermsResponder = null,
        bool siteExists = true,
        bool seededTimezone = false)
    {
        var dbName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ReservationDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<IPassPlanRepository, PassPlanRepository>();
        services.AddApplicationServices();
        services.AddSingleton(Options.Create(new PassPlanDefaultsOptions
        {
            MonthlyMaxPrepaidTerms = 3,
            MonthlyPaymentHoldMinutes = 15,
            MonthlyRenewalWindowDays = 7
        }));
        services.AddSingleton(TimeProvider.System);

        var scopeProxy = System.Reflection.DispatchProxy.Create<IPassPlanFacilityScopePort, DelegatePassScopeProxy>();
        var typedScope = (DelegatePassScopeProxy)scopeProxy;
        typedScope.ValidateOwnerSiteAsyncFunc = (id, _) =>
        {
            if (!siteExists || id != SiteId)
                throw new PassPlanException(404, "SITE_NOT_FOUND", "Parking lot not found.");
            return Task.FromResult(new OwnerSiteScopeResult(OwnerUserId, TenantId, SiteId));
        };
        typedScope.ValidateOwnerDraftScopeAsyncFunc = (id, zoneId, vehicles, excludedZones, _) =>
        {
            if (!siteExists || id != SiteId)
                throw new PassPlanException(404, "SITE_NOT_FOUND", "Parking lot not found.");
            if (zoneId == ForeignZoneId || excludedZones.Contains(ForeignZoneId))
                throw new PassPlanException(422, "UNSUPPORTED_LOT_SCOPE", "Zone is outside this lot.");
            if (vehicles.Contains(PassVehicleType.OVERSIZED))
                throw new PassPlanException(422, "UNSUPPORTED_LOT_SCOPE", "OVERSIZED is not supported.");
            return Task.FromResult(new OwnerSiteScopeResult(OwnerUserId, TenantId, SiteId));
        };
        typedScope.SiteExistsAsyncFunc = (id, _) => Task.FromResult(siteExists && id == SiteId);
        services.AddSingleton(scopeProxy);

        var paymentProxy = System.Reflection.DispatchProxy.Create<IPaymentPassTermsPort, DelegatePaymentTermsProxy>();
        ((DelegatePaymentTermsProxy)paymentProxy).GetActiveFinancialTermsFunc =
            paymentTermsResponder ?? ((_, _, _) => Task.FromResult<IReadOnlyList<PaymentPassFinancialTermDto>>([]));
        services.AddSingleton(paymentProxy);

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<ReservationDbContext>();
        if (seededTimezone)
        {
            db.Set<OperationalPolicyRow>().Add(new OperationalPolicyRow
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SiteId = SiteId,
                Version = 1,
                IsActive = true,
                CancellationTimezone = "Asia/Bangkok",
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
        }

        return (provider, db);
    }

    [Fact]
    public async Task A1_GetSitePassPlans_ReturnsUnconfiguredMonthlyAndQ05BlockedYearlyWithDefaults()
    {
        var (provider, _) = BuildProvider(seededTimezone: true);
        await using var _ = provider;
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new GetSitePassPlansQuery(SiteId));

        Assert.Equal(SiteId, result.SiteId);
        Assert.Equal("Asia/Bangkok", result.TimeZoneId);
        Assert.Equal(3, result.EffectiveOperationalParams.MonthlyMaxPrepaidTerms);
        Assert.Equal(15, result.EffectiveOperationalParams.MonthlyPaymentHoldMinutes);
        Assert.Equal(7, result.EffectiveOperationalParams.MonthlyRenewalWindowDays);
        Assert.Equal(2, result.Plans.Count);

        var monthly = Assert.Single(result.Plans, p => p.ProductType == "MONTHLY");
        Assert.Equal("UNCONFIGURED", monthly.Status);
        Assert.False(monthly.FinancialReadiness.HasCompleteFinancialTerms);
        Assert.Equal("PLAN_NOT_CONFIGURED", monthly.FinancialReadiness.BlockedReason);

        var yearly = Assert.Single(result.Plans, p => p.ProductType == "YEARLY");
        Assert.Equal("UNCONFIGURED", yearly.Status);
        Assert.Equal("YEARLY_POLICY_BLOCKED_Q05", yearly.FinancialReadiness.BlockedReason);
    }

    [Fact]
    public async Task A2_A3_A4_B1_B2_FullMonthlyLifecycleAndGuardsWork()
    {
        var financialTermId = Guid.Parse("ffffffff-0000-4000-8000-000000000001");
        IReadOnlyList<PaymentPassFinancialTermDto> currentTerms = [];
        var (provider, db) = BuildProvider((site, planId, _) => Task.FromResult(
            planId.HasValue
                ? currentTerms.Where(t => t.PlanId == planId.Value).ToArray()
                : currentTerms));
        await using var _ = provider;
        var mediator = provider.GetRequiredService<IMediator>();
        var snapshotService = provider.GetRequiredService<IPassPlanSnapshotService>();

        // A2: Validation guard rejects isAvailableOnly = false
        await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(new SavePassPlanDraftCommand(
            SiteId,
            "MONTHLY",
            OwnerUserId,
            new SavePassPlanDraftRequest(
                null,
                ["CAR"],
                new OrdinaryCoverageRulesDto(true, false, []),
                IsAvailableOnly: false))));

        // B2 & A2 null-string guards throw ValidationException (400), never NullReferenceException
        await Assert.ThrowsAsync<ValidationException>(() => snapshotService.ValidateAndFreezeAsync(
            new ValidateAndFreezePassPlanRequest(SiteId, Guid.NewGuid(), 1, null!, null!)));
        await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(new SavePassPlanDraftCommand(
            SiteId,
            null!,
            OwnerUserId,
            new SavePassPlanDraftRequest(
                null,
                ["CAR"],
                new OrdinaryCoverageRulesDto(true, true, []),
                IsAvailableOnly: true))));

        // A2: Validation guard rejects dayCovered = false & nightCovered = false
        await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(new SavePassPlanDraftCommand(
            SiteId,
            "MONTHLY",
            OwnerUserId,
            new SavePassPlanDraftRequest(
                null,
                ["CAR"],
                new OrdinaryCoverageRulesDto(false, false, []),
                IsAvailableOnly: true))));

        // A2: Facility scope guard rejects foreign zoneId
        var scopeEx = await Assert.ThrowsAsync<PassPlanException>(() => mediator.Send(new SavePassPlanDraftCommand(
            SiteId,
            "MONTHLY",
            OwnerUserId,
            new SavePassPlanDraftRequest(
                ForeignZoneId,
                ["CAR"],
                new OrdinaryCoverageRulesDto(true, true, []),
                IsAvailableOnly: true))));
        Assert.Equal(422, scopeEx.StatusCode);
        Assert.Equal("UNSUPPORTED_LOT_SCOPE", scopeEx.Code);

        // A2: Valid save draft
        var draft = await mediator.Send(new SavePassPlanDraftCommand(
            SiteId,
            "MONTHLY",
            OwnerUserId,
            new SavePassPlanDraftRequest(
                ZoneAId,
                ["CAR", "MOTORCYCLE"],
                new OrdinaryCoverageRulesDto(true, true, []),
                IsAvailableOnly: true)));
        Assert.Equal("DRAFT", draft.Status);
        Assert.Equal(1, draft.TargetRevision);

        // A4: Enable before publish fails
        var prePublishEx = await Assert.ThrowsAsync<PassPlanException>(() => mediator.Send(
            new SetPassPlanEnablementCommand(
                SiteId,
                "MONTHLY",
                OwnerUserId,
                new SetPassPlanEnablementRequest(true, null))));
        Assert.Equal("PLAN_NOT_PUBLISHED", prePublishEx.Code);

        // A3: Publish v1 with Idempotency-Key
        var published1 = await mediator.Send(new PublishPassPlanCommand(
            SiteId,
            "MONTHLY",
            null, // falls back to scope.UserId
            "pub-v1-key",
            new PublishPassPlanRequest("Launch monthly")));
        Assert.Equal(1, published1.PublishedRevision);
        Assert.False(published1.IsEnabled);
        Assert.False(published1.IsReplayed);

        var revRow = await db.ParkingPassPlanRevisions.SingleAsync(r => r.PlanId == published1.PlanId && r.RevisionNumber == 1);
        Assert.Equal(OwnerUserId, revRow.PublishedBy);

        // A3: Idempotent replay returns same revision with IsReplayed = true
        var replayed = await mediator.Send(new PublishPassPlanCommand(
            SiteId,
            "MONTHLY",
            OwnerUserId,
            "pub-v1-key",
            new PublishPassPlanRequest("Launch monthly")));
        Assert.True(replayed.IsReplayed);
        Assert.Equal(1, replayed.PublishedRevision);

        // A3: Reused key with different note returns 409 IDEMPOTENCY_CONFLICT
        var conflictEx = await Assert.ThrowsAsync<PassPlanException>(() => mediator.Send(new PublishPassPlanCommand(
            SiteId,
            "MONTHLY",
            OwnerUserId,
            "pub-v1-key",
            new PublishPassPlanRequest("Changed note"))));
        Assert.Equal(409, conflictEx.StatusCode);
        Assert.Equal("IDEMPOTENCY_CONFLICT", conflictEx.Code);

        // A4: Enable fails with MISSING_FINANCIAL_TERMS when PaymentService has no terms
        var missingTermsEx = await Assert.ThrowsAsync<PassPlanException>(() => mediator.Send(
            new SetPassPlanEnablementCommand(
                SiteId,
                "MONTHLY",
                OwnerUserId,
                new SetPassPlanEnablementRequest(true, 1))));
        Assert.Equal(422, missingTermsEx.StatusCode);
        Assert.Equal("MISSING_FINANCIAL_TERMS", missingTermsEx.Code);

        // B1: Offers remain empty while disabled
        var emptyOffers = await mediator.Send(new GetSitePassOffersQuery(SiteId));
        Assert.True(emptyOffers.YearlyBlockedByQ05);
        Assert.Empty(emptyOffers.Offers);

        // Supply complete financial terms from PaymentService for v1
        currentTerms =
        [
            new PaymentPassFinancialTermDto(
                financialTermId,
                SiteId,
                published1.PlanId,
                PlanRevision: 1,
                FinancialTermRevision: 1,
                ProductType: "MONTHLY",
                Currency: "VND",
                IsActive: true,
                VehiclePrices:
                [
                    new PaymentPassVehiclePriceDto("CAR", 1_500_000m),
                    new PaymentPassVehiclePriceDto("MOTORCYCLE", 180_000m)
                ],
                RefundPolicy: new PaymentPassRefundPolicyDto(100, 50, 100, "HALF_UP_THOUSAND"),
                EffectiveFrom: DateTimeOffset.UtcNow)
        ];

        // A4: Version mismatch -> 409 PLAN_VERSION_CONFLICT
        var versionConflictEx = await Assert.ThrowsAsync<PassPlanException>(() => mediator.Send(
            new SetPassPlanEnablementCommand(
                SiteId,
                "MONTHLY",
                OwnerUserId,
                new SetPassPlanEnablementRequest(true, 99))));
        Assert.Equal(409, versionConflictEx.StatusCode);
        Assert.Equal("PLAN_VERSION_CONFLICT", versionConflictEx.Code);

        // A4: Enable succeeds
        var enabled = await mediator.Send(new SetPassPlanEnablementCommand(
            SiteId,
            "MONTHLY",
            OwnerUserId,
            new SetPassPlanEnablementRequest(true, 1)));
        Assert.True(enabled.IsEnabled);
        Assert.Equal(1, enabled.ActiveRevision);

        // A4: YEARLY enablement blocked by Q05
        var yearlyQ05 = await Assert.ThrowsAsync<PassPlanException>(() => mediator.Send(
            new SetPassPlanEnablementCommand(
                SiteId,
                "YEARLY",
                OwnerUserId,
                new SetPassPlanEnablementRequest(true, null))));
        Assert.Equal(422, yearlyQ05.StatusCode);
        Assert.Equal("YEARLY_POLICY_BLOCKED_Q05", yearlyQ05.Code);

        // B1: Driver offers return filtered CAR offer & full list
        var carOffers = await mediator.Send(new GetSitePassOffersQuery(SiteId, "CAR"));
        var offer = Assert.Single(carOffers.Offers);
        Assert.Equal(published1.PlanId, offer.PlanId);
        Assert.Equal(1_500_000m, offer.FullPeriodPrice);
        Assert.Equal("WHEN_SPACE_AVAILABLE", offer.EntitlementType);
        Assert.False(offer.GuaranteedCapacity);
        Assert.Equal("CALENDAR_MONTH_PRESERVE_ANCHOR_CLAMP_END", offer.AnchorRule);
        Assert.Equal(100, offer.RefundPolicySummary.VoluntaryBeforeStartPercent);

        // B2: In-process IPassPlanSnapshotService freezes snapshot cleanly
        var frozen = await snapshotService.ValidateAndFreezeAsync(
            new ValidateAndFreezePassPlanRequest(SiteId, published1.PlanId, 1, "MONTHLY", "MOTORCYCLE"));
        Assert.Equal(published1.PlanId, frozen.PlanId);
        Assert.Equal(1, frozen.PlanRevision);
        Assert.Equal("MOTORCYCLE", frozen.VehicleType);
        Assert.Equal("Asia/Ho_Chi_Minh", frozen.TimeZoneId);
        Assert.Equal(ZoneAId, frozen.ZoneId);

        // B2: Stale expectedPlanRevision -> 409 PLAN_VERSION_CHANGED
        var staleEx = await Assert.ThrowsAsync<PassPlanException>(() => snapshotService.ValidateAndFreezeAsync(
            new ValidateAndFreezePassPlanRequest(SiteId, published1.PlanId, 2, "MONTHLY", "CAR")));
        Assert.Equal(409, staleEx.StatusCode);
        Assert.Equal("PLAN_VERSION_CHANGED", staleEx.Code);

        // B2: Unsupported vehicleType -> 422 PLAN_NOT_PURCHASABLE
        var unsupportedEx = await Assert.ThrowsAsync<PassPlanException>(() => snapshotService.ValidateAndFreezeAsync(
            new ValidateAndFreezePassPlanRequest(SiteId, published1.PlanId, 1, "MONTHLY", "OVERSIZED")));
        Assert.Equal(422, unsupportedEx.StatusCode);
        Assert.Equal("PLAN_NOT_PURCHASABLE", unsupportedEx.Code);

        // B2: YEARLY blocked by Q05
        var freezeYearlyEx = await Assert.ThrowsAsync<PassPlanException>(() => snapshotService.ValidateAndFreezeAsync(
            new ValidateAndFreezePassPlanRequest(SiteId, published1.PlanId, 1, "YEARLY", "CAR")));
        Assert.Equal(422, freezeYearlyEx.StatusCode);
        Assert.Equal("YEARLY_POLICY_BLOCKED_Q05", freezeYearlyEx.Code);

        // A4: Disable stops B2 purchases
        await mediator.Send(new SetPassPlanEnablementCommand(
            SiteId,
            "MONTHLY",
            OwnerUserId,
            new SetPassPlanEnablementRequest(false, 1)));
        var disabledFreezeEx = await Assert.ThrowsAsync<PassPlanException>(() => snapshotService.ValidateAndFreezeAsync(
            new ValidateAndFreezePassPlanRequest(SiteId, published1.PlanId, 1, "MONTHLY", "CAR")));
        Assert.Equal(422, disabledFreezeEx.StatusCode);
        Assert.Equal("PLAN_NOT_PURCHASABLE", disabledFreezeEx.Code);
    }

    [Fact]
    public async Task A3_PublishWithoutDraft_Throws404PassPlanDraftNotFound()
    {
        var (provider, _) = BuildProvider();
        await using var _ = provider;
        var mediator = provider.GetRequiredService<IMediator>();

        var ex = await Assert.ThrowsAsync<PassPlanException>(() => mediator.Send(
            new PublishPassPlanCommand(SiteId, "MONTHLY", OwnerUserId, null, null)));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal("PASS_PLAN_DRAFT_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task A1_WhenPaymentShowsOutdatedPlanRevision_ReportsFinancialRevisionMismatch()
    {
        var (provider, _) = BuildProvider((_, _, _) => Task.FromResult<IReadOnlyList<PaymentPassFinancialTermDto>>([]));
        await using var _ = provider;
        var mediator = provider.GetRequiredService<IMediator>();

        var draft = await mediator.Send(new SavePassPlanDraftCommand(
            SiteId,
            "MONTHLY",
            OwnerUserId,
            new SavePassPlanDraftRequest(null, ["CAR"], new OrdinaryCoverageRulesDto(true, true, []), true)));
        await mediator.Send(new PublishPassPlanCommand(SiteId, "MONTHLY", OwnerUserId, null, null));
        await mediator.Send(new SavePassPlanDraftCommand(
            SiteId,
            "MONTHLY",
            OwnerUserId,
            new SavePassPlanDraftRequest(null, ["CAR"], new OrdinaryCoverageRulesDto(true, true, []), true)));
        await mediator.Send(new PublishPassPlanCommand(SiteId, "MONTHLY", OwnerUserId, null, null));

        var (providerWithStaleTerm, _) = BuildProvider((_, _, _) => Task.FromResult<IReadOnlyList<PaymentPassFinancialTermDto>>(
        [
            new PaymentPassFinancialTermDto(
                Guid.NewGuid(),
                SiteId,
                draft.PlanId,
                PlanRevision: 1,
                FinancialTermRevision: 1,
                "MONTHLY",
                "VND",
                true,
                [new PaymentPassVehiclePriceDto("CAR", 1_500_000m)],
                new PaymentPassRefundPolicyDto(100, 50, 100, "HALF_UP_THOUSAND"),
                DateTimeOffset.UtcNow)
        ]));
        await using var _2 = providerWithStaleTerm;
        var m2 = providerWithStaleTerm.GetRequiredService<IMediator>();

        await m2.Send(new SavePassPlanDraftCommand(
            SiteId, "MONTHLY", OwnerUserId,
            new SavePassPlanDraftRequest(null, ["CAR"], new OrdinaryCoverageRulesDto(true, true, []), true)));
        var pub2 = await m2.Send(new PublishPassPlanCommand(SiteId, "MONTHLY", OwnerUserId, null, null));
        // Publish a second revision on a new planId matching the term's planId by seeding via repository isn't needed if term points to pub2.PlanId
        var termsForPlan = new PaymentPassFinancialTermDto(
            Guid.NewGuid(), SiteId, pub2.PlanId, 1, 1, "MONTHLY", "VND", true,
            [new PaymentPassVehiclePriceDto("CAR", 1_500_000m)],
            new PaymentPassRefundPolicyDto(100, 50, 100, "HALF_UP_THOUSAND"),
            DateTimeOffset.UtcNow);
        var (p3, db3) = BuildProvider((_, _, _) => Task.FromResult<IReadOnlyList<PaymentPassFinancialTermDto>>([termsForPlan]));
        await using var _3 = p3;
        var repo3 = p3.GetRequiredService<IPassPlanRepository>();
        var domainPlan = ParkingPassPlan.Rehydrate(
            pub2.PlanId, TenantId, SiteId, PassProductType.MONTHLY, "WHEN_SPACE_AVAILABLE",
            false, PassPlanStatus.PUBLISHED, activeRevision: 2, draftRevision: null, draftSpec: null,
            OwnerUserId, OwnerUserId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await repo3.AddPlanAsync(domainPlan, CancellationToken.None);
        await repo3.SaveChangesAsync(CancellationToken.None);

        var summary = await p3.GetRequiredService<IMediator>().Send(new GetSitePassPlansQuery(SiteId));
        var monthly = Assert.Single(summary.Plans, p => p.ProductType == "MONTHLY");
        Assert.False(monthly.FinancialReadiness.HasCompleteFinancialTerms);
        Assert.Equal("FINANCIAL_TERMS_PLAN_REVISION_MISMATCH", monthly.FinancialReadiness.BlockedReason);
    }
}
