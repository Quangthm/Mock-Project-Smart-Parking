using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class ProcessPayOsWebhookCommandHandlerTests
{
    private readonly FakePaymentGatewayPort _gatewayPort = new();
    private readonly FakeProviderWebhookRepository _webhookRepo = new();
    private readonly FakePaymentOrderRepository _orderRepo = new();
    private readonly FakePaymentTransactionRepository _txRepo = new();
    private readonly FakeInvoiceRepository _invoiceRepo = new();
    private readonly FakeEventPublisher _eventPublisher = new();
    private readonly FakeUnitOfWork _uow = new();

    private ProcessPayOsWebhookCommandHandler CreateHandler()
    {
        return new ProcessPayOsWebhookCommandHandler(
            _gatewayPort,
            _webhookRepo,
            _orderRepo,
            _txRepo,
            _invoiceRepo,
            _eventPublisher,
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
    [Fact]
    public async Task Handle_AmountMismatch_ReturnsFailureAndSavesWebhook()
    {
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "123456",
            FinalAmount = 25000,
            Status = PaymentOrderStatus.PENDING
        };
        _orderRepo.Orders.Add(order);

        _gatewayPort.CallbackResult = new PaymentCallbackResult(
            IsValidSignature: true,
            OrderCode: 123456,
            AmountVnd: 25001,
            GatewayTransactionId: "LINK-123",
            BankReference: "REF-123",
            PaymentStatus: "SUCCESS",
            TransactionTime: DateTimeOffset.UtcNow,
            RawPayload: "{}"
        );

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ProcessPayOsWebhookCommand("{}", "valid-sig"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("AMOUNT_MISMATCH", result.ErrorCode);
        Assert.Single(_webhookRepo.Webhooks);
        Assert.Equal(WebhookProcessingState.FAILED, _webhookRepo.Webhooks[0].ProcessingState);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsFailureAndSavesWebhook()
    {
        _gatewayPort.CallbackResult = new PaymentCallbackResult(
            IsValidSignature: true,
            OrderCode: 999999,
            AmountVnd: 25000,
            GatewayTransactionId: "LINK-999",
            BankReference: "REF-999",
            PaymentStatus: "SUCCESS",
            TransactionTime: DateTimeOffset.UtcNow,
            RawPayload: "{}"
        );

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ProcessPayOsWebhookCommand("{}", "valid-sig"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ORDER_NOT_FOUND", result.ErrorCode);
        Assert.Single(_webhookRepo.Webhooks);
        Assert.Equal(WebhookProcessingState.FAILED, _webhookRepo.Webhooks[0].ProcessingState);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Handle_Success_FinalizesInvoice_AndPublishesPaymentSucceededV1()
    {
        var orderId = Guid.NewGuid();
        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "987654",
            FinalAmount = 50000,
            Status = PaymentOrderStatus.PENDING,
            ReferenceId = Guid.NewGuid(),
            ReferenceType = ReferenceType.PARKING_SESSION
        };
        _orderRepo.Orders.Add(order);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PaymentOrderId = orderId,
            InvoiceNumber = "INV-20261102-0999",
            FinalAmount = 50000,
            PricingSnapshot = new InvoicePricingSnapshot
            {
                OrderCode = "987654",
                Status = "PENDING",
                IsSettled = false,
                PlateNumber = "29A-888.88"
            }
        };
        _invoiceRepo.Invoices.Add(invoice);

        _gatewayPort.CallbackResult = new PaymentCallbackResult(
            IsValidSignature: true,
            OrderCode: 987654,
            AmountVnd: 50000,
            GatewayTransactionId: "FT-PAYOS-123456",
            BankReference: "REF-987654",
            PaymentStatus: "SUCCESS",
            TransactionTime: DateTimeOffset.UtcNow,
            RawPayload: "{}"
        );

        var handler = CreateHandler();
        var result = await handler.Handle(new ProcessPayOsWebhookCommand("{}", "valid-sig"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Equal("PAID", invoice.PricingSnapshot.Status);
        Assert.True(invoice.PricingSnapshot.IsSettled);
        Assert.Equal("PAYOS", invoice.PricingSnapshot.PaymentMethod);
        Assert.Equal("FT-PAYOS-123456", invoice.PricingSnapshot.GatewayTransactionId);

        Assert.Single(_eventPublisher.PublishedEvents);
        var evt = Assert.IsType<SmartParking.PaymentService.Domain.Events.PaymentSucceededV1>(_eventPublisher.PublishedEvents[0]);
        Assert.Equal(orderId, evt.PaymentOrderId);
        Assert.Equal("PAYOS", evt.PaymentMethod);
        Assert.Equal(50000, evt.Amount);
        Assert.Equal("29A-888.88", evt.PlateNumber);
    }
}
