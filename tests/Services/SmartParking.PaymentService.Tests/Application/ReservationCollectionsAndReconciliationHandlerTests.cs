using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Application.Usecase.Reservations.Commands.ReconcileReservation;
using SmartParking.PaymentService.Application.Usecase.Reservations.Queries.GetReservationCollections;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Tests.TestDoubles;

namespace SmartParking.PaymentService.Tests.Application;

public class ReservationCollectionsAndReconciliationHandlerTests
{
    [Fact]
    public async Task GetCollections_OrderWithSuccessTransactions_ReturnsSettledWithVerifiedCollections()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var txRepo = new FakePaymentTransactionRepository();

        var orderId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "ORDER-COL-1",
            ReferenceType = ReferenceType.RESERVATION,
            ReferenceId = resId,
            AccountId = accountId,
            TenantId = tenantId,
            SiteId = siteId,
            Amount = 50000m,
            Status = PaymentOrderStatus.PAID
        };
        await orderRepo.AddAsync(order);

        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PaymentOrderId = orderId,
            TenantId = tenantId,
            Amount = 50000m,
            Status = PaymentStatus.SUCCESS,
            GatewayTransactionId = "GATEWAY-TX-1"
        };
        await txRepo.AddAsync(tx);

        var handler = new GetReservationCollectionsQueryHandler(
            orderRepo, txRepo, NullLogger<GetReservationCollectionsQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetReservationCollectionsQuery(orderId, resId), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SETTLED", result.State);
        Assert.Single(result.Collections);
        var item = result.Collections[0];
        Assert.Equal(tx.Id, item.CollectionId);
        Assert.Equal(orderId, item.OrderId);
        Assert.Equal(resId, item.ReservationId);
        Assert.Equal(accountId, item.AccountId);
        Assert.Equal(50000m, item.Amount);
        Assert.Equal("VND", item.Currency);
    }

    [Fact]
    public async Task GetCollections_OrderNotFound_ReturnsClosedNoCollection()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var txRepo = new FakePaymentTransactionRepository();

        var handler = new GetReservationCollectionsQueryHandler(
            orderRepo, txRepo, NullLogger<GetReservationCollectionsQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetReservationCollectionsQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CLOSED_NO_COLLECTION", result.State);
        Assert.Empty(result.Collections);
    }

    [Fact]
    public async Task Reconcile_ValidRequest_CreatesRefundRequestAndRefundTransaction()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var txRepo = new FakePaymentTransactionRepository();
        var refundRepo = new FakeRefundRequestRepository();
        var uow = new FakeUnitOfWork();

        var orderId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "ORDER-REC-1",
            ReferenceType = ReferenceType.RESERVATION,
            ReferenceId = resId,
            TenantId = tenantId,
            Amount = 50000m,
            Status = PaymentOrderStatus.PAID
        };
        await orderRepo.AddAsync(order);

        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PaymentOrderId = orderId,
            TenantId = tenantId,
            Amount = 50000m,
            Status = PaymentStatus.SUCCESS,
            PaymentMethod = PaymentMethod.PAYOS
        };
        await txRepo.AddAsync(tx);

        var handler = new ReconcileReservationCollectionCommandHandler(
            txRepo, orderRepo, refundRepo, uow, NullLogger<ReconcileReservationCollectionCommandHandler>.Instance);

        var requestDto = new ReconcileReservationRequestDto(
            ReservationId: resId,
            CollectionId: tx.Id,
            Amount: 50000m,
            Reason: "LATE_COLLECTION"
        );

        // Act
        var result = await handler.Handle(new ReconcileReservationCollectionCommand(requestDto), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("REFUND_REQUESTED", result.Status);
        Assert.Equal("LATE_COLLECTION", result.Reason);
        Assert.Equal(tx.Id, result.CollectionId);

        // Verify refund request stored in DB
        Assert.Single(refundRepo.RefundRequests);
        var refund = refundRepo.RefundRequests[0];
        Assert.Equal(result.RefundRequestId, refund.Id);
        Assert.Equal(50000m, refund.Amount);
        Assert.Equal("LATE_COLLECTION", refund.Reason);

        // Verify refund transaction stored in DB
        Assert.Equal(2, txRepo.Transactions.Count); // initial tx + refund tx
        var refundTx = txRepo.Transactions.First(t => t.TransactionType == TransactionType.REFUND);
        Assert.Equal(50000m, refundTx.Amount);
        Assert.Equal(PaymentStatus.PENDING, refundTx.Status);
        Assert.True(uow.SaveCount > 0);
    }

    [Fact]
    public async Task Reconcile_DuplicateReason_ReturnsExistingWithoutDuplicating()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var txRepo = new FakePaymentTransactionRepository();
        var refundRepo = new FakeRefundRequestRepository();
        var uow = new FakeUnitOfWork();

        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "ORDER-REC-2",
            ReferenceType = ReferenceType.RESERVATION,
            ReferenceId = Guid.NewGuid(),
            TenantId = tenantId,
            Amount = 50000m
        };
        await orderRepo.AddAsync(order);

        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PaymentOrderId = orderId,
            TenantId = tenantId,
            Amount = 50000m,
            Status = PaymentStatus.SUCCESS
        };
        await txRepo.AddAsync(tx);

        var handler = new ReconcileReservationCollectionCommandHandler(
            txRepo, orderRepo, refundRepo, uow, NullLogger<ReconcileReservationCollectionCommandHandler>.Instance);

        var requestDto = new ReconcileReservationRequestDto(
            ReservationId: order.ReferenceId,
            CollectionId: tx.Id,
            Amount: 50000m,
            Reason: "VOLUNTARY_CANCEL"
        );

        // Act 1: Initial reconcile
        var firstResult = await handler.Handle(new ReconcileReservationCollectionCommand(requestDto), CancellationToken.None);

        // Act 2: Replay reconcile
        var secondResult = await handler.Handle(new ReconcileReservationCollectionCommand(requestDto), CancellationToken.None);

        // Assert
        Assert.Equal(firstResult.RefundRequestId, secondResult.RefundRequestId);
        Assert.Single(refundRepo.RefundRequests); // No duplicate refund created
    }

    [Fact]
    public async Task Reconcile_TransactionNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var txRepo = new FakePaymentTransactionRepository();
        var refundRepo = new FakeRefundRequestRepository();
        var uow = new FakeUnitOfWork();

        var handler = new ReconcileReservationCollectionCommandHandler(
            txRepo, orderRepo, refundRepo, uow, NullLogger<ReconcileReservationCollectionCommandHandler>.Instance);

        var requestDto = new ReconcileReservationRequestDto(
            ReservationId: Guid.NewGuid(),
            CollectionId: Guid.NewGuid(),
            Amount: 50000m,
            Reason: "OVERPAYMENT"
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            handler.Handle(new ReconcileReservationCollectionCommand(requestDto), CancellationToken.None));
        Assert.Equal("TRANSACTION_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task Reconcile_UnsettledTransaction_ThrowsPaymentDomainException()
    {
        // Arrange
        var orderRepo = new FakePaymentOrderRepository();
        var txRepo = new FakePaymentTransactionRepository();
        var refundRepo = new FakeRefundRequestRepository();
        var uow = new FakeUnitOfWork();

        var orderId = Guid.NewGuid();
        var order = new PaymentOrder
        {
            Id = orderId,
            OrderCode = "ORDER-REC-3",
            Amount = 50000m
        };
        await orderRepo.AddAsync(order);

        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PaymentOrderId = orderId,
            Amount = 50000m,
            Status = PaymentStatus.PENDING // Not settled
        };
        await txRepo.AddAsync(tx);

        var handler = new ReconcileReservationCollectionCommandHandler(
            txRepo, orderRepo, refundRepo, uow, NullLogger<ReconcileReservationCollectionCommandHandler>.Instance);

        var requestDto = new ReconcileReservationRequestDto(
            ReservationId: Guid.NewGuid(),
            CollectionId: tx.Id,
            Amount: 50000m,
            Reason: "LATE_COLLECTION"
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            handler.Handle(new ReconcileReservationCollectionCommand(requestDto), CancellationToken.None));
        Assert.Equal("TRANSACTION_NOT_SETTLED", ex.Code);
    }
}
