using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Usecase.InitiateCheckout;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Ports;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class InitiatePaymentCheckoutCommandHandlerTests
{
    private readonly FakePaymentOrderRepository _orderRepo = new();
    private readonly FakePaymentTransactionRepository _txRepo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakePaymentGatewayPort _gatewayPort = new();

    private InitiatePaymentCheckoutCommandHandler CreateHandler()
    {
        return new InitiatePaymentCheckoutCommandHandler(
            _orderRepo,
            _txRepo,
            _uow,
            _gatewayPort,
            NullLogger<InitiatePaymentCheckoutCommandHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsPaymentDomainException()
    {
        var handler = CreateHandler();
        var command = new InitiatePaymentCheckoutCommand(Guid.NewGuid(), "idem-1");

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("ORDER_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task Handle_OrderAlreadyPaid_ThrowsPaymentDomainException()
    {
        var orderId = Guid.NewGuid();
        _orderRepo.Orders.Add(new PaymentOrder
        {
            Id = orderId,
            Status = PaymentOrderStatus.PAID
        });

        var handler = CreateHandler();
        var command = new InitiatePaymentCheckoutCommand(orderId, "idem-1");

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("ORDER_ALREADY_PAID", ex.Code);
    }

    [Fact]
    public async Task Handle_OrderExpired_ThrowsPaymentDomainException()
    {
        var orderId = Guid.NewGuid();
        _orderRepo.Orders.Add(new PaymentOrder
        {
            Id = orderId,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5) // Expired
        });

        var handler = CreateHandler();
        var command = new InitiatePaymentCheckoutCommand(orderId, "idem-1");

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("ORDER_EXPIRED", ex.Code);
    }

    [Fact]
    public async Task Handle_IdempotencyReplay_ReturnsExistingPendingTransaction()
    {
        var orderId = Guid.NewGuid();
        var idemKey = "replayed-key-123";
        var existingTxId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "202610200001",
            FinalAmount = 25000,
            Status = PaymentOrderStatus.PENDING,
            IdempotencyKey = idemKey,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        order.Transactions.Add(new PaymentTransaction
        {
            Id = existingTxId,
            PaymentOrderId = orderId,
            Status = PaymentStatus.PENDING,
            Amount = 25000,
            GatewayTransactionId = "GW-OLD-LINK"
        });

        _orderRepo.Orders.Add(order);

        var handler = CreateHandler();
        var command = new InitiatePaymentCheckoutCommand(orderId, idemKey);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsReplayed);
        Assert.Equal(existingTxId, result.PaymentTransactionId);
        Assert.Equal(25000, result.Amount);
    }

    [Fact]
    public async Task Handle_Success_CallsGatewayAndCreatesTransaction()
    {
        var orderId = Guid.NewGuid();
        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "202610200002",
            FinalAmount = 30000,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        };

        _orderRepo.Orders.Add(order);
        _gatewayPort.InitiateResult = new PaymentInitiateResult(
            IsSuccess: true,
            CheckoutUrl: "https://pay.payos.vn/web/test12345",
            QrCode: "00020101021238...",
            AccountNumber: "123456",
            AccountName: "SMARTPARK",
            Bin: "970422",
            GatewayTransactionId: "LINK-999",
            ErrorCode: null,
            ErrorMessage: null
        );

        var handler = CreateHandler();
        var command = new InitiatePaymentCheckoutCommand(orderId, "new-idem-key");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsReplayed);
        Assert.Equal(30000, result.Amount);
        Assert.Equal("https://pay.payos.vn/web/test12345", result.CheckoutUrl);
        Assert.Equal("PENDING", result.Status);

        Assert.Single(_txRepo.Transactions);
        Assert.Equal(1, _uow.SaveCount);
    }
}
