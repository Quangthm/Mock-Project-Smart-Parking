using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.PaymentService.Application;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.PassPlans;
using SmartParking.PaymentService.Application.Usecase.PassPlans;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Repositories;

namespace SmartParking.PaymentService.Tests.PassPlans;

public sealed class PassFinancialTermsAndRevenueTests
{
    private static readonly Guid OwnerUserId = Guid.Parse("11111111-0000-4000-8000-000000000001");
    private static readonly Guid TenantId = Guid.Parse("22222222-0000-4000-8000-000000000001");
    private static readonly Guid SiteId = Guid.Parse("33333333-0000-4000-8000-000000000001");
    private static readonly Guid PlanId = Guid.Parse("44444444-0000-4000-8000-000000000001");

    private static (ServiceProvider Provider, PaymentDbContext Db) BuildProvider()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PaymentDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddApplicationServices();
        services.AddScoped<ISubscriptionFinancialTermRepository, SubscriptionFinancialTermRepository>();
        services.AddScoped<ISubscriptionClosureCompensationRepository, SubscriptionClosureCompensationRepository>();
        services.AddScoped<IPassRevenueRepository, PassRevenueRepository>();

        var scopeProxy = DispatchProxy.Create<IOwnerSiteAccessPort, DelegateOwnerSiteAccessPortProxy>();
        ((DelegateOwnerSiteAccessPortProxy)scopeProxy).ValidateOwnerSiteAsyncFunc = (id, _) =>
        {
            if (id != SiteId)
                throw new PaymentDomainException("SITE_NOT_FOUND", "Parking lot not found.", "siteId");
            return Task.FromResult(new OwnerSiteScopeResult(OwnerUserId, TenantId, SiteId));
        };
        services.AddSingleton(scopeProxy);

        var provider = services.BuildServiceProvider();
        return (provider, provider.GetRequiredService<PaymentDbContext>());
    }

    [Fact]
    public async Task A5_UpsertFinancialTerms_CreatesRevisionSyncsPolicyRevisionsAndHandlesIdempotencyQ05AndGuards()
    {
        var (provider, db) = BuildProvider();
        await using var _ = provider;
        var mediator = provider.GetRequiredService<IMediator>();

        var m05Policy = new PassRefundPolicyDto(100, 50, 100, "HALF_UP_THOUSAND");

        // YEARLY blocked by Q05
        var yearlyEx = await Assert.ThrowsAsync<PaymentDomainException>(() => mediator.Send(
            new UpsertPassFinancialTermsCommand(
                SiteId,
                PlanId,
                null,
                new UpsertPassFinancialTermsRequest(
                    "YEARLY",
                    1,
                    "VND",
                    [new PassVehiclePriceDto("CAR", 15_000_000m)],
                    m05Policy))));
        Assert.Equal("YEARLY_POLICY_BLOCKED_Q05", yearlyEx.Code);

        // Non-M05 refundPolicy and null/empty RoundingMode rejected cleanly (no NullReferenceException)
        var badRefundEx = await Assert.ThrowsAsync<PaymentDomainException>(() => mediator.Send(
            new UpsertPassFinancialTermsCommand(
                SiteId,
                PlanId,
                null,
                new UpsertPassFinancialTermsRequest(
                    "MONTHLY",
                    1,
                    "VND",
                    [new PassVehiclePriceDto("CAR", 1_500_000m)],
                    new PassRefundPolicyDto(100, 25, 100, "HALF_UP_THOUSAND")))));
        Assert.Equal("INVALID_FINANCIAL_TERMS", badRefundEx.Code);

        var nullRoundingEx = await Assert.ThrowsAsync<PaymentDomainException>(() => mediator.Send(
            new UpsertPassFinancialTermsCommand(
                SiteId,
                PlanId,
                null,
                new UpsertPassFinancialTermsRequest(
                    "MONTHLY",
                    1,
                    "VND",
                    [new PassVehiclePriceDto("CAR", 1_500_000m)],
                    new PassRefundPolicyDto(100, 50, 100, null!)))));
        Assert.Equal("INVALID_FINANCIAL_TERMS", nullRoundingEx.Code);

        // Zero price, fractional VND, null fields, and null items rejected by validator with INVALID_FINANCIAL_TERMS
        var zeroEx = await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(
            new UpsertPassFinancialTermsCommand(
                SiteId,
                PlanId,
                null,
                new UpsertPassFinancialTermsRequest(
                    "MONTHLY",
                    1,
                    "VND",
                    [new PassVehiclePriceDto("CAR", 0m)],
                    m05Policy))));
        Assert.Contains(zeroEx.Errors, e => e.ErrorCode == "INVALID_FINANCIAL_TERMS");

        var fractionalEx = await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(
            new UpsertPassFinancialTermsCommand(
                SiteId,
                PlanId,
                null,
                new UpsertPassFinancialTermsRequest(
                    "MONTHLY",
                    1,
                    "VND",
                    [new PassVehiclePriceDto("CAR", 1_500_000.5m)],
                    m05Policy))));
        Assert.Contains(fractionalEx.Errors, e => e.ErrorCode == "INVALID_FINANCIAL_TERMS");

        var nullFieldsEx = await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(
            new UpsertPassFinancialTermsCommand(
                SiteId,
                PlanId,
                null,
                new UpsertPassFinancialTermsRequest(
                    null!,
                    1,
                    null!,
                    [null!],
                    m05Policy))));
        Assert.Contains(nullFieldsEx.Errors, e => e.ErrorCode == "INVALID_FINANCIAL_TERMS");

        // Happy Path v1 with Idempotency-Key -> authoritative actor scope.UserId persisted
        var req1 = new UpsertPassFinancialTermsRequest(
            "MONTHLY",
            1,
            "VND",
            [
                new PassVehiclePriceDto("CAR", 1_500_000m),
                new PassVehiclePriceDto("MOTORCYCLE", 180_000m)
            ],
            m05Policy);

        var res1 = await mediator.Send(new UpsertPassFinancialTermsCommand(
            SiteId, PlanId, "fin-key-1", req1));

        Assert.Equal(1, res1.FinancialTermRevision);
        Assert.False(res1.IsReplayed);

        var termRow = await db.SubscriptionFinancialTerms.SingleAsync(t => t.Id == res1.FinancialTermId);
        Assert.Equal(OwnerUserId, termRow.CreatedBy);

        var policyRev1 = await db.PolicyRevisions.SingleAsync(p => p.PolicyId == res1.FinancialTermId && p.RevisionNumber == 1);
        Assert.Equal("SUBSCRIPTION", policyRev1.PolicyType);
        Assert.Equal("SITE", policyRev1.ScopeType);
        Assert.Equal(SiteId, policyRev1.ScopeId);
        Assert.Equal(OwnerUserId, policyRev1.CreatedBy);
        Assert.Null(policyRev1.EffectiveTo);

        // Replay with identical payload
        var replay = await mediator.Send(new UpsertPassFinancialTermsCommand(
            SiteId, PlanId, "fin-key-1", req1));
        Assert.True(replay.IsReplayed);
        Assert.Equal(res1.FinancialTermId, replay.FinancialTermId);
        Assert.Equal(1, replay.FinancialTermRevision);

        // Idempotency conflict with different price
        var conflictEx = await Assert.ThrowsAsync<PaymentDomainException>(() => mediator.Send(
            new UpsertPassFinancialTermsCommand(
                SiteId,
                PlanId,
                "fin-key-1",
                new UpsertPassFinancialTermsRequest(
                    "MONTHLY",
                    1,
                    "VND",
                    [new PassVehiclePriceDto("CAR", 1_600_000m)],
                    m05Policy))));
        Assert.Equal("IDEMPOTENCY_CONFLICT", conflictEx.Code);

        // Update v2 closes v1 PolicyRevision and inserts v2
        var res2 = await mediator.Send(new UpsertPassFinancialTermsCommand(
            SiteId,
            PlanId,
            "fin-key-2",
            new UpsertPassFinancialTermsRequest(
                "MONTHLY",
                2,
                "VND",
                [new PassVehiclePriceDto("CAR", 1_750_000m)],
                m05Policy)));
        Assert.Equal(2, res2.FinancialTermRevision);
        var allPolicyRevs = await db.PolicyRevisions
            .Where(p => p.PolicyId == res1.FinancialTermId)
            .OrderBy(p => p.RevisionNumber)
            .ToArrayAsync();
        Assert.Equal(2, allPolicyRevs.Length);
        Assert.NotNull(allPolicyRevs[0].EffectiveTo);
        Assert.Null(allPolicyRevs[1].EffectiveTo);
    }

    [Fact]
    public async Task B3_LookupAndQuoteSnapshot_HandleOptionalPlanIdRevisionStalenessNullGuardsAndQ05()
    {
        var (provider, _) = BuildProvider();
        await using var _ = provider;
        var mediator = provider.GetRequiredService<IMediator>();

        var plan2 = Guid.NewGuid();
        var m05 = new PassRefundPolicyDto(100, 50, 100, "HALF_UP_THOUSAND");

        // Null fields rejected cleanly by validator
        await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(
            new QuotePassFinancialTermsCommand(
                new QuotePassFinancialTermsRequest(SiteId, PlanId, 1, 1, null!, null!))));

        var t1 = await mediator.Send(new UpsertPassFinancialTermsCommand(
            SiteId, PlanId, null,
            new UpsertPassFinancialTermsRequest("MONTHLY", 3, "VND", [new PassVehiclePriceDto("CAR", 1_500_000m)], m05)));
        await mediator.Send(new UpsertPassFinancialTermsCommand(
            SiteId, plan2, null,
            new UpsertPassFinancialTermsRequest("MONTHLY", 1, "VND", [new PassVehiclePriceDto("MOTORCYCLE", 180_000m)], m05)));

        // GET with optional planId omitted returns all active site terms
        var allSiteTerms = await mediator.Send(new GetInternalPassFinancialTermsQuery(SiteId, null));
        Assert.Equal(2, allSiteTerms.Count);

        var singlePlanTerms = await mediator.Send(new GetInternalPassFinancialTermsQuery(SiteId, PlanId));
        var lookedUp = Assert.Single(singlePlanTerms);
        Assert.Equal(t1.FinancialTermId, lookedUp.FinancialTermId);

        // Quote snapshot happy path
        var quote = await mediator.Send(new QuotePassFinancialTermsCommand(
            new QuotePassFinancialTermsRequest(SiteId, PlanId, 3, 1, "MONTHLY", "CAR")));
        Assert.Equal(1_500_000m, quote.FullPeriodPrice);
        Assert.Equal("HALF_UP_THOUSAND", quote.RefundPolicySnapshot.RoundingMode);

        // Stale financialTermRevision -> 409 FINANCIAL_TERMS_CHANGED
        var changedEx = await Assert.ThrowsAsync<PaymentDomainException>(() => mediator.Send(
            new QuotePassFinancialTermsCommand(
                new QuotePassFinancialTermsRequest(SiteId, PlanId, 3, 99, "MONTHLY", "CAR"))));
        Assert.Equal("FINANCIAL_TERMS_CHANGED", changedEx.Code);

        // Unconfigured vehicleType -> 422 FINANCIAL_TERMS_MISSING
        var missingEx = await Assert.ThrowsAsync<PaymentDomainException>(() => mediator.Send(
            new QuotePassFinancialTermsCommand(
                new QuotePassFinancialTermsRequest(SiteId, PlanId, 3, 1, "MONTHLY", "OVERSIZED"))));
        Assert.Equal("FINANCIAL_TERMS_MISSING", missingEx.Code);

        // YEARLY -> 422 YEARLY_POLICY_BLOCKED_Q05
        var q05Ex = await Assert.ThrowsAsync<PaymentDomainException>(() => mediator.Send(
            new QuotePassFinancialTermsCommand(
                new QuotePassFinancialTermsRequest(SiteId, PlanId, 3, 1, "YEARLY", "CAR"))));
        Assert.Equal("YEARLY_POLICY_BLOCKED_Q05", q05Ex.Code);
    }

    [Fact]
    public async Task A6_PassRevenueSummary_UsesCanonicalPostgresStatusesAndHandlesYearlyZero()
    {
        var (provider, db) = BuildProvider();
        await using var _ = provider;
        var mediator = provider.GetRequiredService<IMediator>();
        var now = new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

        var order1 = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SiteId = SiteId,
            OrderCode = "PASS-001",
            OrderType = OrderType.MONTHLY_PASS_FEE,
            ReferenceType = ReferenceType.MONTHLY_PASS,
            ReferenceId = Guid.NewGuid(),
            Amount = 1_500_000m,
            FinalAmount = 1_500_000m,
            Status = PaymentOrderStatus.PAID,
            CreatedAt = now.AddDays(-2)
        };
        var order2CompletedClosureRefund = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SiteId = SiteId,
            OrderCode = "PASS-002",
            OrderType = OrderType.MONTHLY_PASS_FEE,
            ReferenceType = ReferenceType.MONTHLY_PASS,
            ReferenceId = Guid.NewGuid(),
            Amount = 1_500_000m,
            FinalAmount = 1_500_000m,
            Status = PaymentOrderStatus.PAID,
            CreatedAt = now.AddDays(-1)
        };
        var order3Submitted = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SiteId = SiteId,
            OrderCode = "PASS-003",
            OrderType = OrderType.MONTHLY_PASS_FEE,
            ReferenceType = ReferenceType.MONTHLY_PASS,
            ReferenceId = Guid.NewGuid(),
            Amount = 1_500_000m,
            FinalAmount = 1_500_000m,
            Status = PaymentOrderStatus.PAID,
            CreatedAt = now
        };
        var order4Approved = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SiteId = SiteId,
            OrderCode = "PASS-004",
            OrderType = OrderType.MONTHLY_PASS_FEE,
            ReferenceType = ReferenceType.MONTHLY_PASS,
            ReferenceId = Guid.NewGuid(),
            Amount = 1_000_000m,
            FinalAmount = 1_000_000m,
            Status = PaymentOrderStatus.PAID,
            CreatedAt = now
        };
        var ignoredPendingOrder = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SiteId = SiteId,
            OrderCode = "PASS-PENDING",
            OrderType = OrderType.MONTHLY_PASS_FEE,
            ReferenceType = ReferenceType.MONTHLY_PASS,
            ReferenceId = Guid.NewGuid(),
            Amount = 999_000m,
            FinalAmount = 999_000m,
            Status = PaymentOrderStatus.PENDING,
            CreatedAt = now
        };
        var ignoredParkingFee = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SiteId = SiteId,
            OrderCode = "PARK-001",
            OrderType = OrderType.PARKING_FEE,
            ReferenceType = ReferenceType.PARKING_SESSION,
            ReferenceId = Guid.NewGuid(),
            Amount = 50_000m,
            FinalAmount = 50_000m,
            Status = PaymentOrderStatus.PAID,
            CreatedAt = now
        };

        db.PaymentOrders.AddRange(order1, order2CompletedClosureRefund, order3Submitted, order4Approved, ignoredPendingOrder, ignoredParkingFee);
        db.RefundRequests.AddRange(
            // Rejected refund request on order1 MUST NOT be counted as pending or completed
            new RefundRequest
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentOrderId = order1.Id,
                Amount = 500_000m,
                Reason = "Rejected claim",
                Status = "REJECTED",
                HandledByOwner = false
            },
            // Canonical REFUND_COMPLETED (even without transaction row)
            new RefundRequest
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentOrderId = order2CompletedClosureRefund.Id,
                Amount = 750_000m,
                Reason = "Closure completed",
                Status = "REFUND_COMPLETED",
                HandledByOwner = false
            },
            // Canonical REFUND_SUBMITTED
            new RefundRequest
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentOrderId = order3Submitted.Id,
                Amount = 1_500_000m,
                Reason = "Submitted refund",
                Status = "REFUND_SUBMITTED",
                HandledByOwner = false
            },
            // Canonical REFUND_APPROVED
            new RefundRequest
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentOrderId = order4Approved.Id,
                Amount = 1_000_000m,
                Reason = "Approved refund",
                Status = "REFUND_APPROVED",
                HandledByOwner = false
            });
        await db.SaveChangesAsync();

        var summary = await mediator.Send(new GetPassRevenueSummaryQuery(SiteId, null, null, "MONTHLY"));
        Assert.Equal("VND", summary.Currency);
        Assert.Equal(5_500_000m, summary.CollectedAmount);
        Assert.Equal(3_000_000m, summary.CollectedGrantedAmount);
        Assert.Equal(2_500_000m, summary.CollectedAwaitingRefundAmount);
        Assert.Equal(2_500_000m, summary.PendingRefundAmount);
        Assert.Equal(750_000m, summary.RefundedAmount);
        Assert.Equal(2_250_000m, summary.NetSettledRevenue);
        Assert.Equal(2, summary.Counts.FulfilledPasses);
        Assert.Equal(2, summary.Counts.AwaitingRefundOrders);
        Assert.Equal(1, summary.Counts.CompletedRefunds);

        // Date filter excludes order1
        var filtered = await mediator.Send(new GetPassRevenueSummaryQuery(SiteId, now.AddDays(-1), now, null));
        Assert.Equal(4_000_000m, filtered.CollectedAmount);

        // YEARLY returns zeroed VND summary
        var yearly = await mediator.Send(new GetPassRevenueSummaryQuery(SiteId, null, null, "YEARLY"));
        Assert.Equal(0m, yearly.CollectedAmount);
        Assert.Equal("VND", yearly.Currency);
        Assert.Equal(0, yearly.Counts.FulfilledPasses);
    }

    [Fact]
    public async Task B4_CompensatePassClosure_EnforcesPassOrderMismatchNullGuardsUnionOverlapZeroAndCap()
    {
        var (provider, db) = BuildProvider();
        await using var _ = provider;
        var mediator = provider.GetRequiredService<IMediator>();

        var orderId = Guid.NewGuid();
        var passId = Guid.NewGuid();
        var mismatchedRefOrderId = Guid.NewGuid();
        var periodStart = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddDays(30); // 30 days -> 50,000 VND/day on 1,500,000 VND

        db.PaymentOrders.AddRange(
            new PaymentOrder
            {
                Id = orderId,
                TenantId = TenantId,
                SiteId = SiteId,
                OrderCode = "PASS-COMP-1",
                OrderType = OrderType.MONTHLY_PASS_FEE,
                ReferenceType = ReferenceType.MONTHLY_PASS,
                ReferenceId = passId,
                Amount = 1_500_000m,
                FinalAmount = 1_500_000m,
                Status = PaymentOrderStatus.PAID
            },
            new PaymentOrder
            {
                Id = mismatchedRefOrderId,
                TenantId = TenantId,
                SiteId = SiteId,
                OrderCode = "PASS-COMP-WRONG-PASS",
                OrderType = OrderType.MONTHLY_PASS_FEE,
                ReferenceType = ReferenceType.MONTHLY_PASS,
                ReferenceId = Guid.NewGuid(), // different passId
                Amount = 1_500_000m,
                FinalAmount = 1_500_000m,
                Status = PaymentOrderStatus.PAID
            });
        await db.SaveChangesAsync();

        // Validator rejects null fields & null interval elements cleanly (400, never NullReferenceException)
        var nullIntervalEx = await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(
            new CompensatePassClosureCommand(
                new CompensatePassClosureRequest(
                    "k",
                    Guid.NewGuid(),
                    null!,
                    SiteId,
                    TenantId,
                    passId,
                    orderId,
                    periodStart,
                    periodEnd,
                    [null!]))));
        Assert.Contains(nullIntervalEx.Errors, e => e.ErrorCode == "INVALID_CLOSURE_INTERVALS");

        // PASS_ORDER_MISMATCH when ReferenceId != req.PassId
        var mismatchEx = await Assert.ThrowsAsync<PaymentDomainException>(() => mediator.Send(
            new CompensatePassClosureCommand(
                new CompensatePassClosureRequest(
                    "closure-mismatch",
                    Guid.NewGuid(),
                    "TEMPORARY",
                    SiteId,
                    TenantId,
                    passId,
                    mismatchedRefOrderId,
                    periodStart,
                    periodEnd,
                    [new ClosureIntervalDto(periodStart.AddDays(1), periodStart.AddDays(2))]))));
        Assert.Equal("PASS_ORDER_MISMATCH", mismatchEx.Code);

        // 1. Temporary closure days [5..8) = 3 days -> 150,000 VND
        var closure1 = Guid.NewGuid();
        var comp1 = await mediator.Send(new CompensatePassClosureCommand(
            new CompensatePassClosureRequest(
                "closure-1",
                closure1,
                "TEMPORARY",
                SiteId,
                TenantId,
                passId,
                orderId,
                periodStart,
                periodEnd,
                [new ClosureIntervalDto(periodStart.AddDays(5), periodStart.AddDays(8))])));

        Assert.Equal(150_000m, comp1.NewlyCompensatedAmount);
        Assert.Equal(150_000m, comp1.TotalOrderCompensatedAmount);
        Assert.NotNull(comp1.RefundRequestId);
        Assert.Equal("REFUND_REQUESTED", comp1.Status);
        Assert.False(comp1.IsReplayed);

        var rr = await db.RefundRequests.SingleAsync(r => r.Id == comp1.RefundRequestId);
        Assert.False(rr.HandledByOwner);
        Assert.Equal("REFUND_REQUESTED", rr.Status);
        var tx = await db.PaymentTransactions.SingleAsync(t => t.PaymentOrderId == orderId);
        Assert.Equal(TransactionType.REFUND, tx.TransactionType);
        Assert.Equal(PaymentStatus.PENDING, tx.Status);

        // Replay closure-1
        var replay1 = await mediator.Send(new CompensatePassClosureCommand(
            new CompensatePassClosureRequest(
                "closure-1",
                closure1,
                "TEMPORARY",
                SiteId,
                TenantId,
                passId,
                orderId,
                periodStart,
                periodEnd,
                [new ClosureIntervalDto(periodStart.AddDays(5), periodStart.AddDays(8))])));
        Assert.True(replay1.IsReplayed);
        Assert.Equal(comp1.CompensationId, replay1.CompensationId);

        // Key conflict check
        var conflictEx = await Assert.ThrowsAsync<PaymentDomainException>(() => mediator.Send(
            new CompensatePassClosureCommand(
                new CompensatePassClosureRequest(
                    "closure-1",
                    closure1,
                    "TEMPORARY",
                    SiteId,
                    TenantId,
                    passId,
                    orderId,
                    periodStart,
                    periodEnd,
                    [new ClosureIntervalDto(periodStart.AddDays(5), periodStart.AddDays(9))]))));
        Assert.Equal("IDEMPOTENCY_CONFLICT", conflictEx.Code);

        // 2. Overlapping temporary closure [6..12) -> already paid [5..8), so ONLY [8..12) = 4 days -> 200,000 VND
        var comp2 = await mediator.Send(new CompensatePassClosureCommand(
            new CompensatePassClosureRequest(
                "closure-2",
                Guid.NewGuid(),
                "TEMPORARY",
                SiteId,
                TenantId,
                passId,
                orderId,
                periodStart,
                periodEnd,
                [new ClosureIntervalDto(periodStart.AddDays(6), periodStart.AddDays(12))])));

        Assert.Equal(200_000m, comp2.NewlyCompensatedAmount);
        Assert.Equal(350_000m, comp2.TotalOrderCompensatedAmount);
        var interval2 = Assert.Single(comp2.CompensatedIntervals);
        Assert.Equal(periodStart.AddDays(8), interval2.Start);
        Assert.Equal(periodStart.AddDays(12), interval2.End);

        // 3. Fully covered interval [5..10) -> 0 refund due, nullable CompensatedFrom/To
        var compZero = await mediator.Send(new CompensatePassClosureCommand(
            new CompensatePassClosureRequest(
                "closure-zero",
                Guid.NewGuid(),
                "TEMPORARY",
                SiteId,
                TenantId,
                passId,
                orderId,
                periodStart,
                periodEnd,
                [new ClosureIntervalDto(periodStart.AddDays(5), periodStart.AddDays(10))])));

        Assert.Equal(0m, compZero.NewlyCompensatedAmount);
        Assert.Null(compZero.RefundRequestId);
        Assert.Equal("NO_REFUND_DUE", compZero.Status);
        Assert.Empty(compZero.CompensatedIntervals);
        var zeroRow = await db.SubscriptionClosureCompensations.SingleAsync(c => c.Id == compZero.CompensationId);
        Assert.Null(zeroRow.CompensatedFrom);
        Assert.Null(zeroRow.CompensatedTo);

        // 4. Existing external RefundRequest consuming remaining cap -> exceeding order.FinalAmount throws 422 CLOSURE_COMPENSATION_EXCEEDS_ORDER
        db.RefundRequests.Add(new RefundRequest
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            PaymentOrderId = orderId,
            Amount = 1_400_000m,
            Reason = "Voluntary refund already requested",
            Status = "REFUND_APPROVED",
            HandledByOwner = false
        });
        await db.SaveChangesAsync();

        var capEx = await Assert.ThrowsAsync<PaymentDomainException>(() => mediator.Send(
            new CompensatePassClosureCommand(
                new CompensatePassClosureRequest(
                    "closure-exceeds",
                    Guid.NewGuid(),
                    "PERMANENT",
                    SiteId,
                    TenantId,
                    passId,
                    orderId,
                    periodStart,
                    periodEnd,
                    [new ClosureIntervalDto(periodStart.AddDays(20), periodEnd)]))));
        Assert.Equal("CLOSURE_COMPENSATION_EXCEEDS_ORDER", capEx.Code);
    }
}
