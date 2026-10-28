using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Ports;

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
}
