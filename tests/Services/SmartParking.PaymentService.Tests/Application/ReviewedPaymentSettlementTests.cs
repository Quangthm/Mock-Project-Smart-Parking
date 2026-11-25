using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Application.Usecase.Reservations.Commands.ReconcileReservation;
using SmartParking.PaymentService.Application.Usecase.Reservations.Queries.GetReservationCollections;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Events;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Tests.TestDoubles;

namespace SmartParking.PaymentService.Tests.Application;

public sealed class ReviewedPaymentSettlementTests
{
    private readonly FakePaymentGatewayPort _gateway = new();
    private readonly FakePaymentOrderRepository _orders = new();
    private readonly FakePaymentTransactionRepository _transactions = new();
    private readonly FakeRefundRequestRepository _refunds = new();
    private readonly FakeEventPublisher _events = new();
    private readonly FakeUnitOfWork _work = new();

    [Theory]
    [InlineData(5, -1)]
    [InlineData(-5, -10)]
    [InlineData(-5, -1)]
    public async Task ExactCollectionSettlesOnceRegardlessOfHoldDeadline(int holdMinutesFromNow, int transferMinutesFromNow)
    {
        var now = DateTimeOffset.UtcNow;
        var (order, checkout) = await AddOrderAsync(123456, "LINK-123", now.AddMinutes(holdMinutesFromNow));
        var occurredAt = now.AddMinutes(transferMinutesFromNow);

        Assert.True((await DeliverAsync(123456, "LINK-123", "BANK-1", 25_000, occurredAt)).Success);
        Assert.True((await DeliverAsync(123456, "LINK-123", "BANK-1", 25_000, occurredAt)).Success);

        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Same(checkout, Assert.Single(_transactions.Transactions));
        Assert.True(checkout.HasVerifiedProviderCollection());
        Assert.Equal(occurredAt, checkout.PaidAt);
        var payment = Assert.IsType<PaymentSucceededV1>(Assert.Single(_events.PublishedEvents));
        Assert.Equal((checkout.Id, checkout.Id, order.Id, occurredAt),
            (payment.EventId, payment.PaymentTransactionId, payment.PaymentOrderId, payment.OccurredAt));
        Assert.Equal(1, _work.SaveCount);
        Assert.Empty(_refunds.RefundRequests);

        var evidence = Assert.Single((await CollectionsAsync(order.Id)).Collections).Evidence!;
        Assert.Equal((payment.EventId, checkout.Id, "BANK-1", checkout.ProviderVerifiedAt!.Value, occurredAt),
            (evidence.EventId, evidence.PaymentTransactionId, evidence.ProviderReference, evidence.ProviderVerifiedAt, evidence.OccurredAt));
    }

    [Fact]
    public async Task WrongAmountIsPreservedWithoutSettlingOrConsumingCheckout()
    {
        var now = DateTimeOffset.UtcNow;
        var (order, checkout) = await AddOrderAsync(123456, "LINK-123", now.AddMinutes(5));

        Assert.True((await DeliverAsync(123456, "LINK-123", "BANK-1", 12_000, now)).Success);

        Assert.Equal(PaymentOrderStatus.PENDING, order.Status);
        Assert.Equal(PaymentStatus.PENDING, checkout.Status);
        Assert.Empty(_events.PublishedEvents);
        Assert.Empty(_refunds.RefundRequests);
        var nonSettling = Assert.Single((await CollectionsAsync(order.Id)).Collections);
        Assert.Equal(12_000m, nonSettling.Amount);
        Assert.NotEqual(checkout.Id, nonSettling.CollectionId);

        Assert.True((await DeliverAsync(123456, "LINK-123", "BANK-2", 25_000, now)).Success);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Equal(checkout.Id, Assert.IsType<PaymentSucceededV1>(Assert.Single(_events.PublishedEvents)).EventId);
    }

    [Theory]
    [InlineData(PaymentOrderStatus.PAID, "ADDITIONAL_COLLECTION")]
    [InlineData(PaymentOrderStatus.CANCELLED, "ORDER_CANCELLED")]
    public async Task NonSettlingMoneyGetsOneDispositionPerCollection(PaymentOrderStatus status, string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var (order, _) = await AddOrderAsync(123456, "LINK-123", now.AddMinutes(-5));
        order.Status = status;

        foreach (var reference in new[] { "BANK-1", "BANK-2", "BANK-1" })
            Assert.True((await DeliverAsync(123456, "LINK-123", reference, 25_000, now)).Success);

        Assert.Equal(status, order.Status);
        Assert.Empty(_events.PublishedEvents);
        var collections = (await CollectionsAsync(order.Id)).Collections;
        Assert.Equal(2, collections.Count);
        Assert.Equal(2, _refunds.RefundRequests.Count);
        foreach (var collection in collections)
        {
            var stored = Assert.Single(_refunds.RefundRequests, disposition => disposition.CollectionId == collection.CollectionId);
            var replay = await Reconcile().Handle(new(new(order.ReferenceId, collection.CollectionId, collection.Amount, "REPLAY_REASON")), default);
            Assert.Equal(stored.Id, replay.RefundRequestId);
            Assert.Equal(reason, replay.Reason);
            Assert.Equal("REFUND_REQUESTED", replay.Status);
        }
        Assert.Equal(2, _work.SaveCount);
        Assert.DoesNotContain(_transactions.Transactions, tx => tx.TransactionType == TransactionType.REFUND);
    }

    [Fact]
    public async Task WrongLinkAndConflictingReferenceDoNotMutateFinancialEvidence()
    {
        var now = DateTimeOffset.UtcNow;
        var (first, checkout) = await AddOrderAsync(123456, "LINK-123", now.AddMinutes(5));
        var (second, otherCheckout) = await AddOrderAsync(123457, "LINK-456", now.AddMinutes(5));
        Assert.Equal("COLLECTION_BINDING_CONFLICT", (await DeliverAsync(123456, "LINK-456", "BANK-1", 25_000, now)).ErrorCode);
        Assert.True((await DeliverAsync(123456, "LINK-123", "BANK-1", 25_000, now)).Success);
        var acceptedAt = checkout.ProviderVerifiedAt;

        Assert.Equal("COLLECTION_BINDING_CONFLICT", (await DeliverAsync(123456, "LINK-123", "BANK-1", 24_000, now)).ErrorCode);
        Assert.Equal("COLLECTION_BINDING_CONFLICT", (await DeliverAsync(123456, "LINK-123", "BANK-1", 25_000, now.AddSeconds(1))).ErrorCode);
        Assert.Equal("COLLECTION_BINDING_CONFLICT", (await DeliverAsync(123457, "LINK-456", "BANK-1", 25_000, now)).ErrorCode);
        Assert.Equal("COLLECTION_BINDING_CONFLICT", (await DeliverAsync(123456, "LINK-456", "BANK-1", 25_000, now)).ErrorCode);
        Assert.Equal("INVALID_PROVIDER_EVIDENCE", (await DeliverAsync(123456, "LINK-123", " ", 25_000, now)).ErrorCode);
        Assert.Equal(PaymentOrderStatus.PAID, first.Status);
        Assert.Equal(PaymentOrderStatus.PENDING, second.Status);
        Assert.Equal(PaymentStatus.PENDING, otherCheckout.Status);
        Assert.Equal((25_000m, (DateTimeOffset?)now, acceptedAt), (checkout.Amount, checkout.PaidAt, checkout.ProviderVerifiedAt));
        Assert.Equal(2, _transactions.Transactions.Count);
        Assert.Single(_events.PublishedEvents);
    }

    [Fact]
    public async Task CallbackBeforeCheckoutLinkIsRecordedCanRetryWithoutAnUnverifiedWrite()
    {
        var now = DateTimeOffset.UtcNow;
        var (order, checkout) = await AddOrderAsync(123456, null, now.AddMinutes(5));

        var early = await DeliverAsync(123456, "LINK-123", "BANK-1", 25_000, now);
        Assert.False(early.Success);
        Assert.True(early.Retryable);
        Assert.Equal(0, _work.SaveCount);
        Assert.Empty(_events.PublishedEvents);

        checkout.GatewayTransactionId = "LINK-123";
        Assert.True((await DeliverAsync(123456, "LINK-123", "BANK-1", 25_000, now)).Success);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Single(_events.PublishedEvents);
    }

    [Fact]
    public async Task SuccessfulTransactionWithoutProviderEvidenceCannotBeLookedUpOrReconciled()
    {
        var now = DateTimeOffset.UtcNow;
        var (order, checkout) = await AddOrderAsync(123456, "LINK-123", now.AddMinutes(5));
        checkout.MarkSuccess(null, now);

        Assert.Empty((await CollectionsAsync(order.Id)).Collections);
        var error = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            Reconcile().Handle(new(new(order.ReferenceId, checkout.Id, checkout.Amount, "REASON")), default));
        Assert.Equal("QUOTE_BINDING_CONFLICT", error.Code);
        Assert.Empty(_refunds.RefundRequests);
    }

    private async Task<(PaymentOrder Order, PaymentTransaction Checkout)> AddOrderAsync(long code, string? link, DateTimeOffset hold)
    {
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), SiteId = Guid.NewGuid(), AccountId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(), QuoteId = Guid.NewGuid(),
            OrderCode = code.ToString(), IdempotencyKey = Guid.NewGuid().ToString(), OrderType = OrderType.RESERVATION_FEE,
            ReferenceType = ReferenceType.RESERVATION, ReviewedMethod = "PAYOS", ReviewedCurrency = "VND",
            Amount = 25_000, FinalAmount = 25_000, ExpiresAt = hold
        };
        var checkout = new PaymentTransaction
        {
            Id = Guid.NewGuid(), TenantId = order.TenantId, PaymentOrderId = order.Id,
            GatewayTransactionId = link, Amount = order.FinalAmount
        };
        order.Transactions.Add(checkout);
        await _orders.AddAsync(order);
        await _transactions.AddAsync(checkout);
        return (order, checkout);
    }

    private Task<ProcessPayOsWebhookResult> DeliverAsync(long code, string link, string reference, long amount, DateTimeOffset occurredAt)
    {
        _gateway.CallbackResult = new(true, code, amount, link, reference, "SUCCESS", occurredAt, "{}", occurredAt);
        return new ProcessPayOsWebhookCommandHandler(_gateway, new FakeProviderWebhookRepository(), _orders,
            _transactions, new FakeInvoiceRepository(), _refunds, _events, _work,
            NullLogger<ProcessPayOsWebhookCommandHandler>.Instance).Handle(new("{}", "signature"), default);
    }

    private Task<CollectionLookupResponseDto> CollectionsAsync(Guid orderId) =>
        new GetReservationCollectionsQueryHandler(_orders, _transactions, NullLogger<GetReservationCollectionsQueryHandler>.Instance)
            .Handle(new(orderId), default);

    private ReconcileReservationCollectionCommandHandler Reconcile() => new(_transactions, _orders, _refunds, _work,
        NullLogger<ReconcileReservationCollectionCommandHandler>.Instance);
}
