using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Usecase.CollectCashPayment;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Events;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class CollectCashPaymentCommandHandlerTests
{
    private readonly FakePaymentOrderRepository _orderRepo = new();
    private readonly FakePaymentTransactionRepository _txRepo = new();
    private readonly FakeInvoiceRepository _invoiceRepo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeEventPublisher _eventPublisher = new();
    private readonly FakeCurrentActor _actor = new();

    private CollectCashPaymentCommandHandler CreateHandler()
    {
        return new CollectCashPaymentCommandHandler(
            _orderRepo,
            _txRepo,
            _invoiceRepo,
            _uow,
            _eventPublisher,
            _actor,
            NullLogger<CollectCashPaymentCommandHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_HappyPath_CompletesPayment_FinalizesInvoice_PublishesOutboxEvent()
    {
        var orderId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = orderId,
            TenantId = tenantId,
            SiteId = siteId,
            OrderCode = "20261102001",
            Status = PaymentOrderStatus.PENDING,
            FinalAmount = 65000,
            ReferenceId = sessionId,
            ReferenceType = ReferenceType.PARKING_SESSION
        };

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            PaymentOrderId = orderId,
            SessionId = sessionId,
            ReferenceId = sessionId,
            InvoiceNumber = "INV-20261102-0042",
            BaseParkingFee = 45000,
            OverstayPenaltyFee = 20000,
            FinalAmount = 65000,
            PricingSnapshot = new InvoicePricingSnapshot
            {
                OrderCode = "20261102001",
                Status = "PENDING",
                IsSettled = false,
                PlateNumber = "51A-123.45",
                Currency = "VND",
                LineItems = new List<InvoiceLineItem>
                {
                    new() { ItemType = "PENALTY_OVERSTAY", Description = "Quá giờ" }
                }
            }
        };

        _orderRepo.Orders.Add(order);
        _invoiceRepo.Invoices.Add(invoice);

        _actor.Role = "OPERATOR";
        _actor.SiteId = siteId;
        _actor.TenantId = tenantId;

        var handler = CreateHandler();
        var command = new CollectCashPaymentCommand(
            OrderId: orderId,
            AmountReceived: 70000,
            Notes: "Cổng ra thu tiền mặt"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("PAID", result.FinancialStatus.Status);
        Assert.True(result.FinancialStatus.IsSettled);
        Assert.Equal("CASH", result.FinancialStatus.PaymentMethod);

        // Verify order state
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);

        // Verify cash transaction created
        Assert.Single(_txRepo.Transactions);
        var tx = _txRepo.Transactions[0];
        Assert.Equal(PaymentMethod.CASH, tx.PaymentMethod);
        Assert.Equal(PaymentStatus.SUCCESS, tx.Status);
        Assert.Equal(65000, tx.Amount);

        // Verify invoice finalized
        Assert.Equal("PAID", invoice.PricingSnapshot.Status);
        Assert.True(invoice.PricingSnapshot.IsSettled);
        Assert.Equal("CASH", invoice.PricingSnapshot.PaymentMethod);

        // Verify Outbox Event PaymentSucceededV1 published
        Assert.Single(_eventPublisher.PublishedEvents);
        var outboxEvent = Assert.IsType<PaymentSucceededV1>(_eventPublisher.PublishedEvents[0]);
        Assert.Equal(orderId, outboxEvent.PaymentOrderId);
        Assert.Equal("CASH", outboxEvent.PaymentMethod);
        Assert.Equal(65000, outboxEvent.Amount);
        Assert.Equal(45000, outboxEvent.ParkingFee);
        Assert.Equal(20000, outboxEvent.PenaltyFee);
        Assert.Equal("51A-123.45", outboxEvent.PlateNumber);
    }

    [Fact]
    public async Task Handle_UnauthorizedActor_ThrowsUnauthorizedException()
    {
        var orderId = Guid.NewGuid();
        var order = new PaymentOrder { Id = orderId, Status = PaymentOrderStatus.PENDING };
        _orderRepo.Orders.Add(order);

        _actor.IsAuthenticated = false;

        var handler = CreateHandler();
        var command = new CollectCashPaymentCommand(orderId);

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal(PaymentDomainException.Unauthorized, ex.Code);
    }

    [Fact]
    public async Task Handle_DriverRole_ThrowsForbiddenInvoiceAccessException()
    {
        var orderId = Guid.NewGuid();
        var order = new PaymentOrder { Id = orderId, Status = PaymentOrderStatus.PENDING };
        _orderRepo.Orders.Add(order);

        _actor.IsAuthenticated = true;
        _actor.Role = "DRIVER";

        var handler = CreateHandler();
        var command = new CollectCashPaymentCommand(orderId);

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal(PaymentDomainException.ForbiddenInvoiceAccess, ex.Code);
    }

    [Fact]
    public async Task Handle_OperatorSiteMismatch_ThrowsOperatorSiteMismatchException()
    {
        var orderId = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var order = new PaymentOrder { Id = orderId, SiteId = siteA, Status = PaymentOrderStatus.PENDING };
        _orderRepo.Orders.Add(order);

        _actor.IsAuthenticated = true;
        _actor.Role = "OPERATOR";
        _actor.SiteId = siteB; // Different Site!

        var handler = CreateHandler();
        var command = new CollectCashPaymentCommand(orderId);

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal(PaymentDomainException.OperatorSiteMismatch, ex.Code);
    }

    [Fact]
    public async Task Handle_AdminTenantMismatch_ThrowsCrossTenantAccessDeniedException()
    {
        var orderId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var order = new PaymentOrder { Id = orderId, TenantId = tenantA, Status = PaymentOrderStatus.PENDING };
        _orderRepo.Orders.Add(order);

        _actor.IsAuthenticated = true;
        _actor.Role = "ADMIN";
        _actor.TenantId = tenantB; // Different Tenant!

        var handler = CreateHandler();
        var command = new CollectCashPaymentCommand(orderId);

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal(PaymentDomainException.CrossTenantAccessDenied, ex.Code);
    }

    [Fact]
    public async Task Handle_AlreadyPaid_IdempotentReplayWithoutDuplicateEvents()
    {
        var orderId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var order = new PaymentOrder
        {
            Id = orderId,
            TenantId = tenantId,
            SiteId = siteId,
            OrderCode = "20261102001",
            Status = PaymentOrderStatus.PAID,
            FinalAmount = 65000
        };

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            PaymentOrderId = orderId,
            TenantId = tenantId,
            SiteId = siteId,
            InvoiceNumber = "INV-20261102-0042",
            FinalAmount = 65000,
            PricingSnapshot = new InvoicePricingSnapshot
            {
                OrderCode = "20261102001",
                Status = "PAID",
                IsSettled = true,
                PaymentMethod = "CASH"
            }
        };

        _orderRepo.Orders.Add(order);
        _invoiceRepo.Invoices.Add(invoice);

        _actor.Role = "OPERATOR";
        _actor.SiteId = siteId;
        _actor.TenantId = tenantId;

        var handler = CreateHandler();
        var command = new CollectCashPaymentCommand(orderId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("PAID", result.FinancialStatus.Status);
        // Ensure no new transaction and NO outbox event published on replay!
        Assert.Empty(_txRepo.Transactions);
        Assert.Empty(_eventPublisher.PublishedEvents);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsPaymentDomainException()
    {
        var handler = CreateHandler();
        var command = new CollectCashPaymentCommand(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal(PaymentDomainException.OrderNotFound, ex.Code);
    }

    [Fact]
    public void Validator_InvalidModel_FailsValidation()
    {
        var validator = new CollectCashPaymentCommandValidator();

        var invalidCommand = new CollectCashPaymentCommand(Guid.Empty, AmountReceived: -100);
        var result = validator.Validate(invalidCommand);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "OrderId");
        Assert.Contains(result.Errors, e => e.PropertyName == "AmountReceived");
    }
}
