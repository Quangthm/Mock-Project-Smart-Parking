using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Usecase.InitiateCheckout;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class InitiatePaymentCheckoutCommandHandlerTests
{
    private readonly FakePaymentOrderRepository _orderRepo = new();
    private readonly FakePaymentTransactionRepository _txRepo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakePaymentGatewayPort _gatewayPort = new();
    private readonly FakeCurrentActor _actor = new();

    private InitiatePaymentCheckoutCommandHandler CreateHandler(FakeCurrentActor? actor = null)
    {
        return new InitiatePaymentCheckoutCommandHandler(
            _orderRepo,
            _txRepo,
            _uow,
            _gatewayPort,
            actor ?? _actor,
            NullLogger<InitiatePaymentCheckoutCommandHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_DifferentAccount_ThrowsForbiddenPaymentDomainException()
    {
        var orderOwnerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        _orderRepo.Orders.Add(new PaymentOrder
        {
            Id = orderId,
            AccountId = orderOwnerId,
            Status = PaymentOrderStatus.PENDING,
            FinalAmount = 50000,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        });

        var customActor = new FakeCurrentActor
        {
            IsAuthenticated = true,
            UserId = callerId,
            AccountId = callerId,
            IsInternalService = false
        };
        var handler = CreateHandler(customActor);
        var command = new InitiatePaymentCheckoutCommand(orderId, "idem-diff-account");

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("FORBIDDEN_ORDER_ACCESS", ex.Code);
    }

    [Fact]
    public async Task Handle_InternalService_AllowedEvenIfDifferentAccount()
    {
        var orderOwnerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        _orderRepo.Orders.Add(new PaymentOrder
        {
            Id = orderId,
            AccountId = orderOwnerId,
            Status = PaymentOrderStatus.PENDING,
            FinalAmount = 50000,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        });

        var serviceActor = new FakeCurrentActor
        {
            IsAuthenticated = true,
            IsInternalService = true
        };
        var handler = CreateHandler(serviceActor);
        var command = new InitiatePaymentCheckoutCommand(orderId, "idem-service-account");

        var response = await handler.Handle(command, CancellationToken.None);
        Assert.NotNull(response);
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
        Assert.Equal(2, _uow.SaveCount);
    }

    [Fact]
    public async Task Handle_Replay_ReturnsStoredRealQrCodeAndBankData_WithoutFakeStrings()
    {
        var orderId = Guid.NewGuid();
        var idemKey = "replayed-real-key-123";
        var existingTxId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "202610200003",
            FinalAmount = 25000,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        var existingTx = new PaymentTransaction
        {
            Id = existingTxId,
            PaymentOrderId = orderId,
            Status = PaymentStatus.PENDING,
            Amount = 25000,
            GatewayTransactionId = "GW-REAL-LINK",
            CheckoutUrl = "https://pay.payos.vn/web/GW-REAL-LINK",
            QrCode = "0002010102REAL_QR_CODE",
            AccountNumber = "999888777",
            AccountName = "REAL CORP",
            Bin = "970400"
        };
        order.Transactions.Add(existingTx);
        _orderRepo.Orders.Add(order);

        var handler = CreateHandler();
        var command = new InitiatePaymentCheckoutCommand(orderId, idemKey);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsReplayed);
        Assert.Equal("https://pay.payos.vn/web/GW-REAL-LINK", result.CheckoutUrl);
        Assert.Equal("0002010102REAL_QR_CODE", result.QrCode);
        Assert.Equal("999888777", result.AccountNumber);
        Assert.Equal("REAL CORP", result.AccountName);
        Assert.Equal("970400", result.Bin);
    }

    [Fact]
    public async Task Handle_AmbiguousUnknownTransaction_BlocksCheckoutRetry()
    {
        var orderId = Guid.NewGuid();
        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "202610200004",
            FinalAmount = 30000,
            Status = PaymentOrderStatus.PENDING,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        order.Transactions.Add(new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PaymentOrderId = orderId,
            Status = PaymentStatus.UNKNOWN,
            Amount = 30000
        });
        _orderRepo.Orders.Add(order);

        var handler = CreateHandler();
        var command = new InitiatePaymentCheckoutCommand(orderId, "new-attempt");

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("PAYMENT_TRANSACTION_AMBIGUOUS", ex.Code);
    }
}
