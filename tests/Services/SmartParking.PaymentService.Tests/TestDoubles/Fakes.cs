using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Tests.TestDoubles;

public class FakePaymentOrderRepository : IPaymentOrderRepository
{
    public readonly List<PaymentOrder> Orders = new();

    public Task<PaymentOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Orders.FirstOrDefault(x => x.Id == id));
    }

    public Task<PaymentOrder?> GetByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Orders.FirstOrDefault(x => x.OrderCode == orderCode));
    }

    public Task<PaymentOrder?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Orders.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));
    }

    public Task AddAsync(PaymentOrder order, CancellationToken cancellationToken = default)
    {
        Orders.Add(order);
        return Task.CompletedTask;
    }

    public void Update(PaymentOrder order)
    {
        var idx = Orders.FindIndex(x => x.Id == order.Id);
        if (idx >= 0) Orders[idx] = order;
        else Orders.Add(order);
    }
}

public class FakePaymentTransactionRepository : IPaymentTransactionRepository
{
    public readonly List<PaymentTransaction> Transactions = new();

    public Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Transactions.FirstOrDefault(x => x.Id == id));
    }

    public Task<PaymentTransaction?> GetByGatewayTransactionIdAsync(string gatewayTxId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Transactions.FirstOrDefault(x => x.GatewayTransactionId == gatewayTxId));
    }

    public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Transactions.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));
    }

    public Task<IReadOnlyList<PaymentTransaction>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var list = Transactions.Where(x => x.PaymentOrderId == orderId).ToList();
        return Task.FromResult<IReadOnlyList<PaymentTransaction>>(list);
    }

    public Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        Transactions.Add(transaction);
        return Task.CompletedTask;
    }

    public void Update(PaymentTransaction transaction)
    {
        var idx = Transactions.FindIndex(x => x.Id == transaction.Id);
        if (idx >= 0) Transactions[idx] = transaction;
        else Transactions.Add(transaction);
    }
}

public class FakeReservationQuoteRepository : IReservationQuoteRepository
{
    public readonly List<ReservationQuote> Quotes = new();

    public Task<ReservationQuote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Quotes.FirstOrDefault(x => x.Id == id));
    }

    public Task AddAsync(ReservationQuote quote, CancellationToken cancellationToken = default)
    {
        Quotes.Add(quote);
        return Task.CompletedTask;
    }

    public void Update(ReservationQuote quote)
    {
        var idx = Quotes.FindIndex(x => x.Id == quote.Id);
        if (idx >= 0) Quotes[idx] = quote;
        else Quotes.Add(quote);
    }
}

public class FakeRefundRequestRepository : IRefundRequestRepository
{
    public readonly List<RefundRequest> RefundRequests = new();

    public Task<RefundRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(RefundRequests.FirstOrDefault(x => x.Id == id));
    }

    public Task<IReadOnlyList<RefundRequest>> GetByPaymentOrderIdAsync(Guid paymentOrderId, CancellationToken cancellationToken = default)
    {
        var list = RefundRequests.Where(x => x.PaymentOrderId == paymentOrderId).ToList();
        return Task.FromResult<IReadOnlyList<RefundRequest>>(list);
    }

    public Task AddAsync(RefundRequest refundRequest, CancellationToken cancellationToken = default)
    {
        RefundRequests.Add(refundRequest);
        return Task.CompletedTask;
    }

    public void Update(RefundRequest refundRequest)
    {
        var idx = RefundRequests.FindIndex(x => x.Id == refundRequest.Id);
        if (idx >= 0) RefundRequests[idx] = refundRequest;
        else RefundRequests.Add(refundRequest);
    }
}

public class FakeProviderWebhookRepository : IProviderWebhookRepository
{
    public readonly List<ProviderWebhook> Webhooks = new();

    public Task<ProviderWebhook?> GetByProviderAndEventIdAsync(string provider, string eventId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Webhooks.FirstOrDefault(x => x.Provider == provider && x.ProviderEventId == eventId));
    }

    public Task AddAsync(ProviderWebhook webhook, CancellationToken cancellationToken = default)
    {
        Webhooks.Add(webhook);
        return Task.CompletedTask;
    }

    public void Update(ProviderWebhook webhook)
    {
        var idx = Webhooks.FindIndex(x => x.Id == webhook.Id);
        if (idx >= 0) Webhooks[idx] = webhook;
        else Webhooks.Add(webhook);
    }
}

public class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

public class FakePaymentGatewayPort : IPaymentGatewayPort
{
    public string ProviderCode => "PAYOS";

    public PaymentInitiateResult InitiateResult { get; set; } = new(
        IsSuccess: true,
        CheckoutUrl: "https://pay.payos.vn/web/mock",
        QrCode: "0002010102...",
        AccountNumber: "0123456789",
        AccountName: "SMARTPARK",
        Bin: "970422",
        GatewayTransactionId: "LINK-123",
        ErrorCode: null,
        ErrorMessage: null
    );

    public PaymentCallbackResult CallbackResult { get; set; } = new(
        IsValidSignature: true,
        OrderCode: 123456,
        AmountVnd: 25000,
        GatewayTransactionId: "LINK-123",
        BankReference: "REF-123",
        PaymentStatus: "SUCCESS",
        TransactionTime: DateTimeOffset.UtcNow,
        RawPayload: "{}"
    );

    public PaymentVerificationResult VerificationResult { get; set; } = new(
        IsFound: true,
        PaymentStatus: "SUCCESS",
        AmountVnd: 25000,
        GatewayTransactionId: "LINK-123",
        PaidAt: DateTimeOffset.UtcNow
    );

    public PaymentCancellationResult CancellationResult { get; set; } = new(
        IsSuccess: true,
        PaymentStatus: "CANCELLED",
        GatewayTransactionId: "LINK-123",
        ErrorCode: null,
        ErrorMessage: null
    );

    public int QueryTransactionCallCount { get; private set; }

    public Task<PaymentInitiateResult> InitiatePaymentAsync(PaymentInitiateRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(InitiateResult);
    }

    public Task<PaymentCallbackResult> VerifyAndParseWebhookAsync(string rawPayload, string signature, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CallbackResult);
    }

    public Task<PaymentVerificationResult> QueryTransactionAsync(long orderCode, string? gatewayTransactionId, CancellationToken cancellationToken = default)
    {
        QueryTransactionCallCount++;
        return Task.FromResult(VerificationResult);
    }

    public Task<PaymentCancellationResult> CancelPaymentAsync(long orderCode, Guid transactionId, string? gatewayTransactionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CancellationResult);
    }
}

public class FakeInvoiceRepository : IInvoiceRepository
{
    public readonly List<Invoice> Invoices = new();
    private int _sequence = 0;

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Invoices.FirstOrDefault(x => x.Id == id));
    }

    public Task<Invoice?> GetByPaymentOrderIdAsync(Guid paymentOrderId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Invoices.FirstOrDefault(x => x.PaymentOrderId == paymentOrderId));
    }

    public Task<Invoice?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Invoices.FirstOrDefault(x => x.SessionId == sessionId || x.ReferenceId == sessionId));
    }

    public Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Invoices.FirstOrDefault(x => x.InvoiceNumber == invoiceNumber));
    }

    public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        Invoices.Add(invoice);
        return Task.CompletedTask;
    }

    public void Update(Invoice invoice)
    {
        var idx = Invoices.FindIndex(x => x.Id == invoice.Id);
        if (idx >= 0) Invoices[idx] = invoice;
        else Invoices.Add(invoice);
    }

    public Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken = default)
    {
        _sequence++;
        return Task.FromResult($"INV-{DateTimeOffset.UtcNow:yyyyMMdd}-{_sequence:D4}");
    }
    public Task<IReadOnlyList<Invoice>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var list = Invoices
            .Where(i => (i.PaymentOrder != null && i.PaymentOrder.AccountId == accountId) ||
                        (i.PricingSnapshot != null && i.PricingSnapshot.AccountId == accountId))
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<Invoice>>(list);
    }

    public Task<(IReadOnlyList<Invoice> Items, int TotalCount)> GetByAccountIdPagedAsync(
        Guid accountId,
        string? status,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = Invoices
            .Where(i => (i.PaymentOrder != null && i.PaymentOrder.AccountId == accountId) ||
                        (i.PricingSnapshot != null && i.PricingSnapshot.AccountId == accountId));

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(i =>
                (i.PaymentOrder != null && string.Equals(i.PaymentOrder.Status.ToString(), status.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (i.PricingSnapshot != null && string.Equals(i.PricingSnapshot.Status, status.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(i => i.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(i => i.CreatedAt <= toDate.Value);
        }

        var list = query.OrderByDescending(i => i.CreatedAt).ToList();
        var totalCount = list.Count;

        var effectivePageNumber = pageNumber < 1 ? 1 : pageNumber;
        var effectivePageSize = pageSize < 1 ? 10 : pageSize;

        var paged = list
            .Skip((effectivePageNumber - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToList();

        return Task.FromResult<(IReadOnlyList<Invoice> Items, int TotalCount)>((paged, totalCount));
    }
}

public class FakePdfInvoiceGenerator : IPdfInvoiceGenerator
{
    public byte[] DummyPdfBytes { get; set; } = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A }; // %PDF-1.7
    public CustomerInvoiceResponseDto? LastInvoicePassed { get; private set; }
    public int CallCount { get; private set; }

    public Task<byte[]> GenerateInvoicePdfAsync(CustomerInvoiceResponseDto invoice, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastInvoicePassed = invoice;
        return Task.FromResult(DummyPdfBytes);
    }
}

public class FakeCurrentActor : SmartParking.PaymentService.Application.Common.Interfaces.ICurrentActor
{
    public bool IsAuthenticated { get; set; } = true;
    public Guid? UserId { get; set; }
    public Guid? AccountId
    {
        get => _accountId ?? UserId;
        set => _accountId = value;
    }
    private Guid? _accountId;
    public string? Role { get; set; } = "DRIVER";
    public Guid? TenantId { get; set; }
    public Guid? SiteId { get; set; }
    public bool IsInternalService { get; set; } = false;
}

public class FakeEventPublisher : SmartParking.PaymentService.Application.Common.Interfaces.IEventPublisher
{
    public readonly List<object> PublishedEvents = new();

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
    {
        PublishedEvents.Add(@event);
        return Task.CompletedTask;
    }
}


public class FakePublishEndpoint : MassTransit.IPublishEndpoint
{
    public readonly List<object> PublishedMessages = new();

    public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish<T>(T message, MassTransit.IPipe<MassTransit.PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish<T>(T message, MassTransit.IPipe<MassTransit.PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish(object message, CancellationToken cancellationToken = default)
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default)
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish(object message, MassTransit.IPipe<MassTransit.PublishContext> publishPipe, CancellationToken cancellationToken = default)
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish(object message, Type messageType, MassTransit.IPipe<MassTransit.PublishContext> publishPipe, CancellationToken cancellationToken = default)
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(values);
        return Task.CompletedTask;
    }

    public Task Publish<T>(object values, MassTransit.IPipe<MassTransit.PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(values);
        return Task.CompletedTask;
    }

    public Task Publish<T>(object values, MassTransit.IPipe<MassTransit.PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class
    {
        PublishedMessages.Add(values);
        return Task.CompletedTask;
    }

    public MassTransit.ConnectHandle ConnectPublishObserver(MassTransit.IPublishObserver observer) => throw new NotImplementedException();
}
