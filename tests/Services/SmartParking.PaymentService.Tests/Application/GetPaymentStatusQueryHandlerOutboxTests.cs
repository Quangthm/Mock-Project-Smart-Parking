using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Application.Usecase.GetPaymentStatus;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Events;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class GetPaymentStatusQueryHandlerOutboxTests
{
    private readonly FakePaymentTransactionRepository _txRepo = new();
    private readonly FakePaymentOrderRepository _orderRepo = new();
    private readonly FakeInvoiceRepository _invoiceRepo = new();
    private readonly FakeProviderWebhookRepository _webhookRepo = new();
    private readonly FakeEventPublisher _eventPublisher = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakePaymentGatewayPort _gatewayPort = new();

    private GetPaymentStatusQueryHandler CreateHandler()
    {
        return new GetPaymentStatusQueryHandler(
            _txRepo,
            _orderRepo,
            _invoiceRepo,
            _eventPublisher,
            _uow,
            _gatewayPort,
            NullLogger<GetPaymentStatusQueryHandler>.Instance
        );
    }

    private ProcessPayOsWebhookCommandHandler CreateWebhookHandler()
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
    public async Task Handle_ReconcileSuccess_UpdatesInvoiceAndPublishesPaymentSucceededV1()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var txId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var paidAt = DateTimeOffset.UtcNow;

        var order = new PaymentOrder
        {
            Id = orderId,
            TenantId = tenantId,
            SiteId = siteId,
            AccountId = accountId,
            OrderCode = "20261104001",
            FinalAmount = 35000,
            Status = PaymentOrderStatus.PENDING,
            ReferenceId = sessionId,
            ReferenceType = ReferenceType.PARKING_SESSION
        };
        _orderRepo.Orders.Add(order);

        var tx = new PaymentTransaction
        {
            Id = txId,
            TenantId = tenantId,
            PaymentOrderId = orderId,
            PaymentOrder = order,
            Amount = 35000,
            Status = PaymentStatus.PENDING,
            PaymentMethod = PaymentMethod.PAYOS,
            GatewayTransactionId = "PAYOS-TX-888"
        };
        _txRepo.Transactions.Add(tx);

        var invoice = new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            PaymentOrderId = orderId,
            SessionId = sessionId,
            ReferenceId = sessionId,
            InvoiceNumber = "INV-20261104-001",
            BaseParkingFee = 30000,
            OverstayPenaltyFee = 5000,
            FinalAmount = 35000,
            PricingSnapshot = new InvoicePricingSnapshot
            {
                OrderCode = "20261104001",
                Status = "PENDING",
                IsSettled = false,
                PlateNumber = "30A-999.99",
                Currency = "VND",
                LineItems = new List<InvoiceLineItem>
                {
                    new()
                    {
                        ItemType = "PENALTY_OVERSTAY",
                        Description = "Quá giờ 30 phút",
                        Amount = 5000
                    }
                }
            }
        };
        _invoiceRepo.Invoices.Add(invoice);

        _gatewayPort.VerificationResult = new PaymentVerificationResult(
            IsFound: true,
            PaymentStatus: "SUCCESS",
            AmountVnd: 35000,
            GatewayTransactionId: "PAYOS-TX-888",
            PaidAt: paidAt
        );

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);

        // Assert - Return DTO
        Assert.NotNull(result);
        Assert.True(result.IsAuthoritative);
        Assert.Equal("SUCCESS", result.Status);
        Assert.Equal(paidAt, result.PaidAt);
        Assert.Equal(1, _gatewayPort.QueryTransactionCallCount);

        // Assert - Transaction State
        Assert.Equal(PaymentStatus.SUCCESS, tx.Status);
        Assert.Equal(paidAt, tx.PaidAt);
        Assert.Equal("PAYOS-TX-888", tx.GatewayTransactionId);

        // Assert - Order State
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Equal(paidAt, order.UpdatedAt);

        // Assert - Invoice Snapshot Update
        Assert.Equal("PAID", invoice.PricingSnapshot.Status);
        Assert.True(invoice.PricingSnapshot.IsSettled);
        Assert.Equal(paidAt, invoice.PricingSnapshot.PaidAt);
        Assert.Equal("PAYOS", invoice.PricingSnapshot.PaymentMethod);
        Assert.Equal(txId.ToString(), invoice.PricingSnapshot.TransactionReference);
        Assert.Equal("PAYOS-TX-888", invoice.PricingSnapshot.GatewayTransactionId);

        // Assert - Atomic Outbox Event (PaymentSucceededV1)
        Assert.Single(_eventPublisher.PublishedEvents);
        var evt = Assert.IsType<PaymentSucceededV1>(_eventPublisher.PublishedEvents[0]);
        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.Equal(paidAt, evt.OccurredAt);
        Assert.Equal("20261104001", evt.CorrelationId);
        Assert.Equal(orderId, evt.PaymentOrderId);
        Assert.Equal(txId, evt.PaymentTransactionId);
        Assert.Equal("PAYOS", evt.PaymentMethod);
        Assert.Equal("PAYOS-TX-888", evt.GatewayReference);
        Assert.Equal(tenantId, evt.TenantId);
        Assert.Equal(siteId, evt.SiteId);
        Assert.Equal("PARKING_SESSION", evt.ReferenceType);
        Assert.Equal(sessionId, evt.ReferenceId);
        Assert.Equal("30A-999.99", evt.PlateNumber);
        Assert.Equal(accountId, evt.AccountId);
        Assert.Equal(35000m, evt.Amount);
        Assert.Equal(30000m, evt.ParkingFee);
        Assert.Equal(5000m, evt.PenaltyFee);
        Assert.Equal("Quá giờ 30 phút", evt.PenaltyReason);
        Assert.Equal("VND", evt.Currency);

        // Assert - Atomic Unit of Work commit
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Handle_ReconcileSuccess_WhenOrderAlreadyPaid_DoesNotDuplicateOutboxEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var txId = Guid.NewGuid();
        var paidAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "20261104002",
            FinalAmount = 25000,
            Status = PaymentOrderStatus.PAID,
            UpdatedAt = paidAt
        };
        _orderRepo.Orders.Add(order);

        var tx = new PaymentTransaction
        {
            Id = txId,
            PaymentOrderId = orderId,
            PaymentOrder = order,
            Amount = 25000,
            Status = PaymentStatus.PENDING,
            PaymentMethod = PaymentMethod.PAYOS
        };
        _txRepo.Transactions.Add(tx);

        _gatewayPort.VerificationResult = new PaymentVerificationResult(
            IsFound: true,
            PaymentStatus: "SUCCESS",
            AmountVnd: 25000,
            GatewayTransactionId: "PAYOS-TX-999",
            PaidAt: paidAt
        );

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);

        // Assert
        Assert.Equal("SUCCESS", result.Status);
        Assert.Equal(PaymentStatus.SUCCESS, tx.Status);
        Assert.Empty(_eventPublisher.PublishedEvents); // No duplicate outbox event staged
        Assert.Equal(1, _uow.SaveCount); // Transaction state saved
    }

    [Fact]
    public async Task Handle_ReconcileCancelled_MarksTransactionCancelled_DoesNotPublishEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var txId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "20261104003",
            FinalAmount = 20000,
            Status = PaymentOrderStatus.PENDING
        };
        _orderRepo.Orders.Add(order);

        var tx = new PaymentTransaction
        {
            Id = txId,
            PaymentOrderId = orderId,
            PaymentOrder = order,
            Amount = 20000,
            Status = PaymentStatus.PENDING,
            PaymentMethod = PaymentMethod.PAYOS
        };
        _txRepo.Transactions.Add(tx);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PaymentOrderId = orderId,
            FinalAmount = 20000,
            PricingSnapshot = new InvoicePricingSnapshot
            {
                Status = "PENDING",
                IsSettled = false
            }
        };
        _invoiceRepo.Invoices.Add(invoice);

        _gatewayPort.VerificationResult = new PaymentVerificationResult(
            IsFound: true,
            PaymentStatus: "CANCELLED",
            AmountVnd: 20000,
            GatewayTransactionId: "PAYOS-TX-CANCEL",
            PaidAt: null
        );

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);

        // Assert
        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal(PaymentStatus.CANCELLED, tx.Status);
        Assert.Equal(PaymentOrderStatus.PENDING, order.Status);
        Assert.False(invoice.PricingSnapshot.IsSettled);
        Assert.Equal("PENDING", invoice.PricingSnapshot.Status);
        Assert.Empty(_eventPublisher.PublishedEvents);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Handle_ReconcileSuccess_WhenInvoiceNull_PublishesOutboxEventWithOrderFallbacks()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var txId = Guid.NewGuid();
        var paidAt = DateTimeOffset.UtcNow;

        var order = new PaymentOrder
        {
            Id = orderId,
            TenantId = tenantId,
            SiteId = siteId,
            OrderCode = "20261104004",
            FinalAmount = 40000,
            Status = PaymentOrderStatus.PENDING,
            ReferenceType = ReferenceType.PARKING_SESSION,
            ReferenceId = Guid.NewGuid()
        };
        _orderRepo.Orders.Add(order);

        var tx = new PaymentTransaction
        {
            Id = txId,
            TenantId = tenantId,
            PaymentOrderId = orderId,
            PaymentOrder = order,
            Amount = 40000,
            Status = PaymentStatus.PENDING,
            PaymentMethod = PaymentMethod.PAYOS
        };
        _txRepo.Transactions.Add(tx);

        // Explicitly NO invoice in _invoiceRepo

        _gatewayPort.VerificationResult = new PaymentVerificationResult(
            IsFound: true,
            PaymentStatus: "SUCCESS",
            AmountVnd: 40000,
            GatewayTransactionId: "PAYOS-TX-NO-INV",
            PaidAt: paidAt
        );

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);

        // Assert
        Assert.Equal("SUCCESS", result.Status);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Equal(PaymentStatus.SUCCESS, tx.Status);

        Assert.Single(_eventPublisher.PublishedEvents);
        var evt = Assert.IsType<PaymentSucceededV1>(_eventPublisher.PublishedEvents[0]);
        Assert.Null(evt.PlateNumber);
        Assert.Equal(40000m, evt.ParkingFee); // Fallback to order.FinalAmount
        Assert.Null(evt.PenaltyFee);
        Assert.Null(evt.PenaltyReason);
        Assert.Equal("VND", evt.Currency);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Handle_TransactionAlreadySuccess_ShortCircuitsWithoutQueryingGatewayOrPublishingEvent()
    {
        // Arrange
        var txId = Guid.NewGuid();
        var tx = new PaymentTransaction
        {
            Id = txId,
            PaymentOrderId = Guid.NewGuid(),
            Amount = 50000,
            Status = PaymentStatus.SUCCESS,
            PaidAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        _txRepo.Transactions.Add(tx);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);

        // Assert
        Assert.True(result.IsAuthoritative);
        Assert.Equal("SUCCESS", result.Status);
        Assert.Equal(0, _gatewayPort.QueryTransactionCallCount);
        Assert.Empty(_eventPublisher.PublishedEvents);
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Handle_NonNumericOrderCode_DoesNotQueryGateway()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var txId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "INVALID-CODE",
            FinalAmount = 15000,
            Status = PaymentOrderStatus.PENDING
        };
        _orderRepo.Orders.Add(order);

        var tx = new PaymentTransaction
        {
            Id = txId,
            PaymentOrderId = orderId,
            PaymentOrder = order,
            Amount = 15000,
            Status = PaymentStatus.PENDING,
            PaymentMethod = PaymentMethod.PAYOS
        };
        _txRepo.Transactions.Add(tx);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);

        // Assert
        Assert.False(result.IsAuthoritative);
        Assert.Equal("PENDING", result.Status);
        Assert.Equal(0, _gatewayPort.QueryTransactionCallCount);
        Assert.Empty(_eventPublisher.PublishedEvents);
        Assert.Equal(0, _uow.SaveCount);
    }

    [Fact]
    public async Task Handle_RaceCondition_QueryHandlerWinsRace_ThenWebhookArrives_DoesNotDuplicateOutboxEvent()
    {
        // Scenario 1: Active query reconciliation wins race, sets order PAID and stages outbox event.
        // Delayed webhook arrives later: must recognize already-PAID order and NOT stage a second event.
        var orderId = Guid.NewGuid();
        var txId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var paidAt = DateTimeOffset.UtcNow;

        var order = new PaymentOrder
        {
            Id = orderId,
            TenantId = tenantId,
            SiteId = siteId,
            OrderCode = "20261104005",
            FinalAmount = 50000,
            Status = PaymentOrderStatus.PENDING,
            ReferenceType = ReferenceType.PARKING_SESSION,
            ReferenceId = sessionId
        };
        var tx = new PaymentTransaction
        {
            Id = txId,
            TenantId = tenantId,
            PaymentOrderId = orderId,
            PaymentOrder = order,
            Amount = 50000,
            Status = PaymentStatus.PENDING,
            PaymentMethod = PaymentMethod.PAYOS,
            GatewayTransactionId = "PAYOS-GATEWAY-005"
        };
        order.Transactions.Add(tx);
        _orderRepo.Orders.Add(order);
        _txRepo.Transactions.Add(tx);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            PaymentOrderId = orderId,
            SessionId = sessionId,
            ReferenceId = sessionId,
            InvoiceNumber = "INV-20261104-005",
            FinalAmount = 50000,
            PricingSnapshot = new InvoicePricingSnapshot
            {
                OrderCode = "20261104005",
                Status = "PENDING",
                IsSettled = false,
                PlateNumber = "51F-123.45"
            }
        };
        _invoiceRepo.Invoices.Add(invoice);

        // 1. Setup Query reconciliation result
        _gatewayPort.VerificationResult = new PaymentVerificationResult(
            IsFound: true,
            PaymentStatus: "SUCCESS",
            AmountVnd: 50000,
            GatewayTransactionId: "PAYOS-GATEWAY-005",
            PaidAt: paidAt
        );

        var queryHandler = CreateHandler();
        var queryResult = await queryHandler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);

        // Assert Step 1: Query reconciled successfully
        Assert.True(queryResult.IsAuthoritative);
        Assert.Equal("SUCCESS", queryResult.Status);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.True(invoice.PricingSnapshot.IsSettled);
        Assert.Single(_eventPublisher.PublishedEvents);

        // 2. Delayed Webhook arrives
        _gatewayPort.CallbackResult = new PaymentCallbackResult(
            IsValidSignature: true,
            OrderCode: 20261104005,
            AmountVnd: 50000,
            GatewayTransactionId: "PAYOS-GATEWAY-005",
            BankReference: "BANK-REF-005",
            PaymentStatus: "SUCCESS",
            TransactionTime: paidAt,
            RawPayload: "{\"orderCode\":20261104005}"
        );

        var webhookHandler = CreateWebhookHandler();
        var webhookResult = await webhookHandler.Handle(
            new ProcessPayOsWebhookCommand("{\"orderCode\":20261104005}", "valid-signature"),
            CancellationToken.None
        );

        // Assert Step 2: Webhook handled idempotently, NO duplicate event staged!
        Assert.True(webhookResult.Success);
        Assert.Single(_eventPublisher.PublishedEvents); // Strictly 1 event in total!
        Assert.IsType<PaymentSucceededV1>(_eventPublisher.PublishedEvents[0]);
    }

    [Fact]
    public async Task Handle_RaceCondition_WebhookWinsRace_ThenQueryHandlerRuns_DoesNotDuplicateOutboxEvent()
    {
        // Scenario 2: Webhook arrives first, marks order PAID, tx SUCCESS, and stages outbox event.
        // Kiosk/driver polling arrives after: tx is already SUCCESS -> fast short-circuit without extra gateway query or event.
        var orderId = Guid.NewGuid();
        var txId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var paidAt = DateTimeOffset.UtcNow;

        var order = new PaymentOrder
        {
            Id = orderId,
            TenantId = tenantId,
            SiteId = siteId,
            OrderCode = "20261104006",
            FinalAmount = 60000,
            Status = PaymentOrderStatus.PENDING,
            ReferenceType = ReferenceType.PARKING_SESSION,
            ReferenceId = sessionId
        };
        var tx = new PaymentTransaction
        {
            Id = txId,
            TenantId = tenantId,
            PaymentOrderId = orderId,
            PaymentOrder = order,
            Amount = 60000,
            Status = PaymentStatus.PENDING,
            PaymentMethod = PaymentMethod.PAYOS,
            GatewayTransactionId = "PAYOS-GATEWAY-006"
        };
        order.Transactions.Add(tx);
        _orderRepo.Orders.Add(order);
        _txRepo.Transactions.Add(tx);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            PaymentOrderId = orderId,
            SessionId = sessionId,
            ReferenceId = sessionId,
            InvoiceNumber = "INV-20261104-006",
            FinalAmount = 60000,
            PricingSnapshot = new InvoicePricingSnapshot
            {
                OrderCode = "20261104006",
                Status = "PENDING",
                IsSettled = false,
                PlateNumber = "51G-999.88"
            }
        };
        _invoiceRepo.Invoices.Add(invoice);

        // 1. Webhook arrives first
        _gatewayPort.CallbackResult = new PaymentCallbackResult(
            IsValidSignature: true,
            OrderCode: 20261104006,
            AmountVnd: 60000,
            GatewayTransactionId: "PAYOS-GATEWAY-006",
            BankReference: "BANK-REF-006",
            PaymentStatus: "SUCCESS",
            TransactionTime: paidAt,
            RawPayload: "{\"orderCode\":20261104006}"
        );

        var webhookHandler = CreateWebhookHandler();
        var webhookResult = await webhookHandler.Handle(
            new ProcessPayOsWebhookCommand("{\"orderCode\":20261104006}", "valid-signature"),
            CancellationToken.None
        );

        // Assert Step 1: Webhook staged event
        Assert.True(webhookResult.Success);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Equal(PaymentStatus.SUCCESS, tx.Status);
        Assert.True(invoice.PricingSnapshot.IsSettled);
        Assert.Single(_eventPublisher.PublishedEvents);

        // 2. Client query runs afterwards
        var queryHandler = CreateHandler();
        var queryResult = await queryHandler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);

        // Assert Step 2: Query returned immediately, zero gateway calls, NO duplicate event!
        Assert.True(queryResult.IsAuthoritative);
        Assert.Equal("SUCCESS", queryResult.Status);
        Assert.Equal(0, _gatewayPort.QueryTransactionCallCount); // Short-circuited from database
        Assert.Single(_eventPublisher.PublishedEvents); // Strictly 1 event in total!
    }

    [Fact]
    public async Task Handle_RaceCondition_WebhookSettledOrder_QueryHandlerRunsOnPendingTransaction_MarksTxSuccessWithoutDuplicateEvent()
    {
        // Scenario 3: Order was settled by webhook on a previous attempt, but client checks a pending transaction.
        // QueryHandler queries gateway, sees SUCCESS, marks tx SUCCESS, but skips event publishing because order is already PAID.
        var orderId = Guid.NewGuid();
        var pendingTxId = Guid.NewGuid();
        var paidAt = DateTimeOffset.UtcNow;

        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "20261104007",
            FinalAmount = 25000,
            Status = PaymentOrderStatus.PAID // Order already settled
        };
        var pendingTx = new PaymentTransaction
        {
            Id = pendingTxId,
            PaymentOrderId = orderId,
            PaymentOrder = order,
            Amount = 25000,
            Status = PaymentStatus.PENDING, // This transaction was still pending
            PaymentMethod = PaymentMethod.PAYOS
        };
        _orderRepo.Orders.Add(order);
        _txRepo.Transactions.Add(pendingTx);

        _gatewayPort.VerificationResult = new PaymentVerificationResult(
            IsFound: true,
            PaymentStatus: "SUCCESS",
            AmountVnd: 25000,
            GatewayTransactionId: "PAYOS-GATEWAY-007",
            PaidAt: paidAt
        );

        var queryHandler = CreateHandler();
        var result = await queryHandler.Handle(new GetPaymentStatusQuery(pendingTxId), CancellationToken.None);

        // Assert: Transaction marked SUCCESS, but NO event published
        Assert.Equal("SUCCESS", result.Status);
        Assert.Equal(PaymentStatus.SUCCESS, pendingTx.Status);
        Assert.Empty(_eventPublisher.PublishedEvents);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Handle_IdempotentPolling_MultipleQueryHandlerCalls_PublishesOutboxEventOnlyOnce()
    {
        // Scenario 4: Fast kiosk polling - 3 queries in rapid succession.
        // Only the first call reconciles with gateway and publishes outbox event.
        // Calls 2 and 3 must short-circuit without extra queries or events.
        var orderId = Guid.NewGuid();
        var txId = Guid.NewGuid();
        var paidAt = DateTimeOffset.UtcNow;

        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "20261104008",
            FinalAmount = 30000,
            Status = PaymentOrderStatus.PENDING,
            ReferenceType = ReferenceType.PARKING_SESSION,
            ReferenceId = Guid.NewGuid()
        };
        var tx = new PaymentTransaction
        {
            Id = txId,
            PaymentOrderId = orderId,
            PaymentOrder = order,
            Amount = 30000,
            Status = PaymentStatus.PENDING,
            PaymentMethod = PaymentMethod.PAYOS
        };
        _orderRepo.Orders.Add(order);
        _txRepo.Transactions.Add(tx);

        _gatewayPort.VerificationResult = new PaymentVerificationResult(
            IsFound: true,
            PaymentStatus: "SUCCESS",
            AmountVnd: 30000,
            GatewayTransactionId: "PAYOS-GATEWAY-008",
            PaidAt: paidAt
        );

        var handler = CreateHandler();

        // Poll 1: Reconciles with gateway
        var res1 = await handler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);
        Assert.True(res1.IsAuthoritative);
        Assert.Equal(1, _gatewayPort.QueryTransactionCallCount);
        Assert.Single(_eventPublisher.PublishedEvents);

        // Poll 2: Instant return from DB
        var res2 = await handler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);
        Assert.True(res2.IsAuthoritative);
        Assert.Equal(1, _gatewayPort.QueryTransactionCallCount); // Still 1
        Assert.Single(_eventPublisher.PublishedEvents); // Still 1

        // Poll 3: Instant return from DB
        var res3 = await handler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);
        Assert.True(res3.IsAuthoritative);
        Assert.Equal(1, _gatewayPort.QueryTransactionCallCount); // Still 1
        Assert.Single(_eventPublisher.PublishedEvents); // Still 1
    }
}

