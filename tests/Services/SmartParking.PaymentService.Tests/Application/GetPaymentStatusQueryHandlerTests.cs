using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Usecase.GetPaymentStatus;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Ports;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class GetPaymentStatusQueryHandlerTests
{
    private readonly FakePaymentTransactionRepository _txRepo = new();
    private readonly FakePaymentOrderRepository _orderRepo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakePaymentGatewayPort _gatewayPort = new();

    private GetPaymentStatusQueryHandler CreateHandler()
    {
        return new GetPaymentStatusQueryHandler(
            _txRepo,
            _orderRepo,
            _uow,
            _gatewayPort,
            NullLogger<GetPaymentStatusQueryHandler>.Instance
        );
    }

    [Fact]
    public async Task Handle_TransactionNotFound_ThrowsPaymentDomainException()
    {
        var handler = CreateHandler();
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            handler.Handle(new GetPaymentStatusQuery(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("TRANSACTION_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task Handle_TransactionAlreadySuccess_ReturnsAuthoritativeWithoutReconciliation()
    {
        var txId = Guid.NewGuid();
        var tx = new PaymentTransaction
        {
            Id = txId,
            PaymentOrderId = Guid.NewGuid(),
            Amount = 25000,
            Status = PaymentStatus.SUCCESS,
            PaidAt = DateTimeOffset.UtcNow
        };

        _txRepo.Transactions.Add(tx);

        var handler = CreateHandler();
        var result = await handler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);

        Assert.True(result.IsAuthoritative);
        Assert.Equal("SUCCESS", result.Status);
        Assert.Equal(0, _gatewayPort.QueryTransactionCallCount);
    }

    [Fact]
    public async Task Handle_PendingTransaction_ReconcilesWithGateway()
    {
        var txId = Guid.NewGuid();
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "202610200001",
            FinalAmount = 25000,
            Status = PaymentOrderStatus.PENDING
        };
        var tx = new PaymentTransaction
        {
            Id = txId,
            PaymentOrderId = order.Id,
            PaymentOrder = order,
            Amount = 25000,
            Status = PaymentStatus.PENDING,
            GatewayTransactionId = "GW-LINK-1"
        };

        _txRepo.Transactions.Add(tx);
        _orderRepo.Orders.Add(order);

        _gatewayPort.VerificationResult = new PaymentVerificationResult(
            IsFound: true,
            PaymentStatus: "SUCCESS",
            AmountVnd: 25000,
            GatewayTransactionId: "GW-LINK-1",
            PaidAt: DateTimeOffset.UtcNow
        );

        var handler = CreateHandler();
        var result = await handler.Handle(new GetPaymentStatusQuery(txId), CancellationToken.None);

        Assert.True(result.IsAuthoritative);
        Assert.Equal("SUCCESS", result.Status);
        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Equal(1, _gatewayPort.QueryTransactionCallCount);
        Assert.Equal(1, _uow.SaveCount);
    }
}
