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

    [Fact]
    public async Task Handle_PaidOrder_ReceivesDuplicateSameTransaction_ReplaysSuccessWithoutDuplicateProcessing()
    {
        var orderId = Guid.NewGuid();
        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "555111",
            FinalAmount = 30000,
            Status = PaymentOrderStatus.PAID
        };
        var settledTx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PaymentOrderId = orderId,
            Amount = 30000,
            Status = PaymentStatus.SUCCESS,
            GatewayTransactionId = "FT-SAME-111"
        };
        order.Transactions.Add(settledTx);
        _orderRepo.Orders.Add(order);
        _txRepo.Transactions.Add(settledTx);

        _gatewayPort.CallbackResult = new PaymentCallbackResult(
            IsValidSignature: true,
            OrderCode: 555111,
            AmountVnd: 30000,
            GatewayTransactionId: "FT-SAME-111",
            BankReference: "REF-555111",
            PaymentStatus: "SUCCESS",
            TransactionTime: DateTimeOffset.UtcNow,
            RawPayload: "{}"
        );

        var handler = CreateHandler();
        var result = await handler.Handle(new ProcessPayOsWebhookCommand("{}", "valid-sig"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("already settled", result.Message);
        Assert.Empty(_eventPublisher.PublishedEvents); // No duplicate outbox event
    }

    [Fact]
    public async Task Handle_PaidOrder_ReceivesNewSecondTransaction_CreatesNewTransactionAndPublishesEvent_NoSilentDrop()
    {
        var orderId = Guid.NewGuid();
        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "555222",
            FinalAmount = 30000,
            Status = PaymentOrderStatus.PAID
        };
        var firstTx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PaymentOrderId = orderId,
            Amount = 30000,
            Status = PaymentStatus.SUCCESS,
            GatewayTransactionId = "FT-FIRST-TX"
        };
        order.Transactions.Add(firstTx);
        _orderRepo.Orders.Add(order);
        _txRepo.Transactions.Add(firstTx);

        // A second payment callback arrives with a DIFFERENT gateway transaction ID (e.g. customer paid twice)
        _gatewayPort.CallbackResult = new PaymentCallbackResult(
            IsValidSignature: true,
            OrderCode: 555222,
            AmountVnd: 30000,
            GatewayTransactionId: "FT-SECOND-TX",
            BankReference: "REF-555222-SECOND",
            PaymentStatus: "SUCCESS",
            TransactionTime: DateTimeOffset.UtcNow,
            RawPayload: "{}"
        );

        var handler = CreateHandler();
        var result = await handler.Handle(new ProcessPayOsWebhookCommand("{}", "valid-sig"), CancellationToken.None);

        Assert.True(result.Success);
        // Ensure a second transaction is recorded and not silently dropped!
        Assert.Equal(2, _txRepo.Transactions.Count); // firstTx + new secondTx
        Assert.Single(_eventPublisher.PublishedEvents); // event published for the extra collection
        var evt = Assert.IsType<SmartParking.PaymentService.Domain.Events.PaymentSucceededV1>(_eventPublisher.PublishedEvents[0]);
        Assert.Equal("FT-SECOND-TX", evt.GatewayReference);
    }

    [Fact]
    public async Task Handle_ReservationDepositOrder_PublishesPaymentSucceededV1WithReservationMetadata()
    {
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "777888",
            Amount = 50000,
            FinalAmount = 50000,
            Status = PaymentOrderStatus.PENDING,
            OrderType = OrderType.RESERVATION_FEE,
            ReferenceType = ReferenceType.RESERVATION,
            ReferenceId = reservationId,
            AccountId = accountId,
            TenantId = tenantId,
            SiteId = siteId,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        _orderRepo.Orders.Add(order);

        _gatewayPort.CallbackResult = new PaymentCallbackResult(
            IsValidSignature: true,
            OrderCode: 777888,
            AmountVnd: 50000,
            GatewayTransactionId: "FT-RESERVATION-TX",
            BankReference: "REF-777888",
            PaymentStatus: "SUCCESS",
            TransactionTime: DateTimeOffset.UtcNow,
            RawPayload: "{}"
        );

        var handler = CreateHandler();
        var result = await handler.Handle(new ProcessPayOsWebhookCommand("{}", "valid-sig"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Single(_eventPublisher.PublishedEvents);

        var evt = Assert.IsType<SmartParking.PaymentService.Domain.Events.PaymentSucceededV1>(_eventPublisher.PublishedEvents[0]);
        Assert.Equal(orderId, evt.PaymentOrderId);
        Assert.Equal("RESERVATION", evt.ReferenceType);
        Assert.Equal(reservationId, evt.ReferenceId);
        Assert.Equal(accountId, evt.AccountId);
        Assert.Equal(50000m, evt.Amount);
        Assert.Equal(50000m, evt.ParkingFee);
        Assert.Equal("FT-RESERVATION-TX", evt.GatewayReference);
    }
}
