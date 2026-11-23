using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Application.Reservations;
using SmartParking.PaymentService.Application.Usecase.CreatePaymentOrder;
using SmartParking.PaymentService.Application.Usecase.InitiateCheckout;
using SmartParking.PaymentService.Application.Usecase.Reservations.Commands.EnsureReservationDepositOrder;
using SmartParking.PaymentService.Application.Usecase.Reservations.Queries.GetReservationCollections;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Repositories;
using SmartParking.PaymentService.Tests.TestDoubles;

namespace SmartParking.PaymentService.Tests.Persistence;

public sealed class ReviewedOrderDatabaseTests
{
    [ReviewedOrderDatabaseFact]
    public async Task ReviewedOrderKeepsTheQuoteBindingAndCheckoutEnforcesRuntimeCapability()
    {
        await using var db = Open();
        var payOs = await SeedQuoteAsync(db, "PAYOS", covered: false);
        // Alternate method represents previously persisted commercial evidence, not a gateway or issuance capability.
        var other = await SeedQuoteAsync(db, "MOMO", covered: false);
        var handler = Ensure(db);

        var paid = await handler.Handle(new(payOs), default);
        var alternative = await handler.Handle(new(other), default);
        Assert.Equal("CREATED", paid.State);
        Assert.Equal("CREATED", alternative.State);
        Assert.Equal(paid, await handler.Handle(new(payOs), default));
        Assert.Equal(alternative, await handler.Handle(new(other), default));

        var id = alternative.OrderId!.Value;
        var stored = await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == id);
        Assert.Equal((other.QuoteId, other.TermsVersion, "MOMO", "VND", other.ExpiresAt),
            (stored.QuoteId!.Value, stored.TermsVersion, stored.ReviewedMethod, stored.ReviewedCurrency, stored.ExpiresAt!.Value));
        var changed = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            handler.Handle(new(other with { PaymentMethod = "PAYOS" }), default));
        Assert.Equal("QUOTE_BINDING_CONFLICT", changed.Code);

        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_reviewed_binding",
            $"UPDATE payment_orders SET reviewed_currency = NULL WHERE id = {id}");
        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_reviewed_binding",
            $"UPDATE payment_orders SET order_type = 'PARKING_FEE' WHERE id = {id}");
        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_reviewed_binding",
            $"UPDATE payment_orders SET terms_version = NULL WHERE id = {id}");
        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_zero_disposition",
            $"UPDATE payment_orders SET zero_disposition_reason = 'not zero' WHERE id = {id}");
        await AssertDatabaseError(db, PostgresErrorCodes.UniqueViolation, "idx_reviewed_order_quote",
            $"UPDATE payment_orders SET quote_id = {payOs.QuoteId} WHERE id = {id}");
        await AssertDatabaseError(db, PostgresErrorCodes.UniqueViolation, "idx_reviewed_order_reservation",
            $"UPDATE payment_orders SET reference_id = {payOs.ReservationId} WHERE id = {id}");

        var gateway = new FakePaymentGatewayPort();
        var checkout = new InitiatePaymentCheckoutCommandHandler(new PaymentOrderRepository(db),
            new PaymentTransactionRepository(db), new UnitOfWork(db), gateway,
            new FakeCurrentActor { IsInternalService = true }, NullLogger<InitiatePaymentCheckoutCommandHandler>.Instance);
        var unsupported = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            checkout.Handle(new(id, IdempotencyKey: null, PaymentMethod: PaymentMethod.MOMO), default));
        Assert.Equal("UNSUPPORTED_PAYMENT_METHOD", unsupported.Code);
        var otherCurrency = await SeedQuoteAsync(db, "PAYOS", covered: false, currency: "USD");
        var otherCurrencyOrder = await handler.Handle(new(otherCurrency), default);
        var unsupportedCurrency = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            checkout.Handle(new(otherCurrencyOrder.OrderId!.Value, IdempotencyKey: null), default));
        Assert.Equal("UNSUPPORTED_PAYMENT_CURRENCY", unsupportedCurrency.Code);
        Assert.Null(gateway.LastInitiateRequest);
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentOrderId == id || t.PaymentOrderId == otherCurrencyOrder.OrderId));
    }

    [ReviewedOrderDatabaseFact]
    public async Task ExpiredProvisioningHasNoBusinessStatusAndCannotBecomeAnOrder()
    {
        await using var db = Open();
        var active = await SeedQuoteAsync(db, "PAYOS", covered: false);
        var expired = await SeedQuoteAsync(db, "PAYOS", covered: false);
        expired = expired with { ExpiresAt = expired.ExpiresAt.AddHours(-1) };
        var handler = Ensure(db);
        var accepted = await handler.Handle(new(active), default);
        var closed = await handler.Handle(new(expired), default);
        Assert.Equal(("CLOSED_NO_ORDER", (Guid?)null), (closed.State, closed.OrderId));
        Assert.Equal(closed, await handler.Handle(new(expired), default));
        var resurrect = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            handler.Handle(new(expired with { ExpiresAt = active.ExpiresAt }), default));
        Assert.Equal("QUOTE_BINDING_CONFLICT", resurrect.Code);

        var orders = new PaymentOrderRepository(db);
        var fence = await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.IdempotencyKey == expired.OperationId.ToString());
        Assert.True(fence.IsProvisioningFence);
        Assert.Null(fence.Status);
        Assert.Null(await orders.GetByIdAsync(fence.Id));
        Assert.Null(await orders.GetByOrderCodeAsync(fence.OrderCode));
        Assert.Null(await orders.FindReviewedOrderIdAsync(fence.OrderCode, default));
        Assert.True(await orders.HasReviewedReservationAsync(expired.ReservationId, default));
        var create = new CreatePaymentOrderCommandHandler(orders, new UnitOfWork(db));
        var keyConflict = await Assert.ThrowsAsync<PaymentDomainException>(() => create.Handle(new(new(
            fence.TenantId, fence.SiteId, fence.ReferenceId, fence.ReferenceType, fence.FinalAmount,
            fence.AccountId, fence.IdempotencyKey)), default));
        Assert.Equal("IDEMPOTENCY_PAYLOAD_MISMATCH", keyConflict.Code);
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentOrderId == fence.Id));
        Assert.False(await db.Invoices.AnyAsync(i => i.PaymentOrderId == fence.Id));
        var collections = new GetReservationCollectionsQueryHandler(orders, new PaymentTransactionRepository(db),
            NullLogger<GetReservationCollectionsQueryHandler>.Instance);
        Assert.Equal("CLOSED_NO_COLLECTION", (await collections.Handle(new(fence.Id), default)).State);
        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_provisioning_fence",
            $"UPDATE payment_orders SET status = 'CANCELLED' WHERE id = {fence.Id}");

        var commercial = (await orders.GetByIdAsync(accepted.OrderId!.Value))!;
        Assert.False(commercial.IsProvisioningFence);
        commercial.MarkCancelled(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(PaymentOrderStatus.CANCELLED, (await orders.GetByIdAsync(commercial.Id))!.Status);
        Assert.Equal(commercial.Id, await orders.FindReviewedOrderIdAsync(commercial.OrderCode, default));
        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_provisioning_fence",
            $"UPDATE payment_orders SET status = NULL WHERE id = {commercial.Id}");
    }

    [ReviewedOrderDatabaseFact]
    public async Task ZeroValueReceiptDispositionIsProviderIndependentAndIdempotent()
    {
        await using var db = Open();
        var quote = await SeedQuoteAsync(db, "MOMO", covered: true);
        var handler = Ensure(db);
        var accepted = await handler.Handle(new(quote), default);
        Assert.Equal("CREATED", accepted.State);
        Assert.True(accepted.ReceiptId.HasValue);
        Assert.Equal(accepted, await handler.Handle(new(quote), default));

        var id = accepted.OrderId!.Value;
        var receiptId = accepted.ReceiptId!.Value;
        var orders = new PaymentOrderRepository(db);
        var disposition = new RecordZeroDispositionHandler(orders, new UnitOfWork(db));
        var command = new RecordZeroDisposition(quote.OperationId, quote.ReservationId, quote.AccountId, id, receiptId,
            quote.QuoteId, quote.TermsVersion, "HOLD_EXPIRED");
        var mismatch = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            disposition.Handle(command with { ReceiptId = Guid.NewGuid() }, default));
        Assert.Equal("QUOTE_BINDING_CONFLICT", mismatch.Code);

        Assert.Equal(receiptId, await disposition.Handle(command, default));
        Assert.Equal(receiptId, await disposition.Handle(command with { Reason = "OTHER_REASON" }, default));
        var stored = await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == id);
        Assert.Equal("MOMO", stored.ReviewedMethod);
        Assert.Equal("HOLD_EXPIRED", stored.ZeroDispositionReason);
        Assert.Equal(0, stored.FinalAmount);
        var receipt = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == receiptId);
        Assert.Equal(id, receipt.PaymentOrderId);
        Assert.Equal(0, receipt.FinalAmount);
        Assert.True(receipt.PricingSnapshot.IsSettled);
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentOrderId == id));
        Assert.False(await db.RefundRequests.AnyAsync(r => r.PaymentOrderId == id));

        var gateway = new FakePaymentGatewayPort();
        var checkout = new InitiatePaymentCheckoutCommandHandler(orders, new PaymentTransactionRepository(db),
            new UnitOfWork(db), gateway, new FakeCurrentActor { IsInternalService = true },
            NullLogger<InitiatePaymentCheckoutCommandHandler>.Instance);
        var noCheckout = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            checkout.Handle(new(id, IdempotencyKey: null, PaymentMethod: PaymentMethod.MOMO), default));
        Assert.Equal("ORDER_ALREADY_PAID", noCheckout.Code);
        Assert.Null(gateway.LastInitiateRequest);
    }

    private static PaymentDbContext Open() => new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("SMARTPARK_PAYMENT_ORDER_TEST_DB")!).Options);

    private static EnsureReservationDepositOrderCommandHandler Ensure(PaymentDbContext db) =>
        new(new PaymentOrderRepository(db), new ReservationQuoteRepository(db), new UnitOfWork(db),
            NullLogger<EnsureReservationDepositOrderCommandHandler>.Instance);

    private static async Task AssertDatabaseError(PaymentDbContext db, string code, string constraint, FormattableString sql)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
        Assert.Equal(code, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    private static async Task<EnsureReservationDepositOrderRequestDto> SeedQuoteAsync(
        PaymentDbContext db, string method, bool covered, string currency = "VND")
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var binding = new ReviewedQuoteBinding(Guid.NewGuid(), "51A12345", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            now.AddHours(2), now.AddHours(3), method, new QuoteCoverage(covered, "fixture-coverage"), "fixture-layout",
            Guid.NewGuid(), 1, Guid.NewGuid(), 1);
        var booked = 20_000L;
        var coveredAmount = covered ? booked : 0;
        var pricing = new PricingCalculationResult(currency, binding.Start, binding.End, "Asia/Ho_Chi_Minh", 60, 60, 60, 0,
            false, booked, booked, MonetaryRoundingMode.HALF_UP_WHOLE_VND, Guid.NewGuid(), "Test tariff", 1, []);
        var proof = new ReviewedQuoteProof(Guid.NewGuid(), $"rq1-{Guid.NewGuid():N}", "ISSUED", now.AddMinutes(5), binding,
            Guid.NewGuid().ToString(), booked, coveredAmount, booked - coveredAmount, currency, "FULL", "Test disclosure", pricing);
        db.ReservationQuotes.Add(new ReservationQuote
        {
            Id = proof.QuoteId, AccountId = binding.AccountId, TenantId = binding.TenantId, SiteId = binding.SiteId,
            RequestedSlotId = binding.RequestedSlotId, StartTime = binding.Start, EndTime = binding.End,
            BookedAmount = proof.BookedAmount, DepositAmount = proof.DepositAmount, Currency = proof.Currency,
            TermsVersion = proof.QuoteVersion, ValidUntil = proof.ValidUntil, ReviewedEvidence = JsonSerializer.Serialize(proof),
            PrepaymentMode = PrepaymentMode.FULL, CoverageVersion = binding.Coverage.Version
        });
        await db.SaveChangesAsync();

        return new(Guid.NewGuid(), Guid.NewGuid(), binding.AccountId, binding.TenantId, binding.SiteId,
            proof.QuoteId, proof.QuoteVersion, proof.DepositAmount, proof.Currency, method, now.AddMinutes(10),
            covered ? ZeroQuoteSelection.From(binding) : null);
    }
}
