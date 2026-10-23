using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Ports;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class ProcessPayOsWebhookCommandHandlerTests
{
    private readonly FakePaymentGatewayPort _gatewayPort = new();
    private readonly FakeProviderWebhookRepository _webhookRepo = new();
    private readonly FakePaymentOrderRepository _orderRepo = new();
    private readonly FakePaymentTransactionRepository _txRepo = new();
    private readonly FakeUnitOfWork _uow = new();

    private ProcessPayOsWebhookCommandHandler CreateHandler()
    {
        return new ProcessPayOsWebhookCommandHandler(
            _gatewayPort,
            _webhookRepo,
            _orderRepo,
            _txRepo,
            _uow,
            NullLogger<ProcessPayOsWebhookCommandHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_InvalidSignature_ReturnsFailure()
    {
        _gatewayPort.CallbackResult = new PaymentCallbackResult(
            IsValidSignature: false,
            OrderCode: 123,
            AmountVnd: 25000,
            GatewayTransactionId: null,
            BankReference: null,
            PaymentStatus: "FAILED",
            TransactionTime: DateTimeOffset.UtcNow,
            RawPayload: "{}"
        );

        var handler = CreateHandler();
        var result = await handler.Handle(new ProcessPayOsWebhookCommand("{}", "invalid-sig"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_WEBHOOK_SIGNATURE", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_DuplicateWebhook_ReturnsSuccessWithoutReProcessing()
    {
        _gatewayPort.CallbackResult = new PaymentCallbackResult(
            IsValidSignature: true,
            OrderCode: 123456,
            AmountVnd: 25000,
            GatewayTransactionId: "LINK-123",
            BankReference: "REF-123",
            PaymentStatus: "SUCCESS",
            TransactionTime: DateTimeOffset.UtcNow,
            RawPayload: "{}"
        );

        _webhookRepo.Webhooks.Add(new ProviderWebhook
        {
            Provider = "PAYOS",
            ProviderEventId = "REF-123",
            ProcessingState = WebhookProcessingState.PROCESSED
        });

        var handler = CreateHandler();
        var result = await handler.Handle(new ProcessPayOsWebhookCommand("{}", "valid-sig"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Idempotent", result.Message);
        Assert.Empty(_orderRepo.Orders);
    }

    [Fact]
    public async Task Handle_ValidWebhook_UpdatesOrderAndTransactionToPaid()
    {
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "123456",
            FinalAmount = 25000,
            Status = PaymentOrderStatus.PENDING
        };
        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PaymentOrderId = order.Id,
            Amount = 25000,
            Status = PaymentStatus.PENDING
        };
        order.Transactions.Add(tx);

        _orderRepo.Orders.Add(order);
        _txRepo.Transactions.Add(tx);

        _gatewayPort.CallbackResult = new PaymentCallbackResult(
            IsValidSignature: true,
            OrderCode: 123456,
            AmountVnd: 25000,
            GatewayTransactionId: "LINK-123",
            BankReference: "REF-123",
            PaymentStatus: "SUCCESS",
            TransactionTime: DateTimeOffset.UtcNow,
            RawPayload: "{}"
        );

        var handler = CreateHandler();
        var result = await handler.Handle(new ProcessPayOsWebhookCommand("{}", "valid-sig"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Equal(PaymentStatus.SUCCESS, tx.Status);
        Assert.Equal(1, _uow.SaveCount);
    }
}
