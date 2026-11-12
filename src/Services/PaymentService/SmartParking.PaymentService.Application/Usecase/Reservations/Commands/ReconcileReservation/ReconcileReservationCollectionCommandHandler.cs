using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.ReconcileReservation;

public sealed class ReconcileReservationCollectionCommandHandler
    : IRequestHandler<ReconcileReservationCollectionCommand, ReconcileReservationResponseDto>
{
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IRefundRequestRepository _refundRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReconcileReservationCollectionCommandHandler> _logger;

    public ReconcileReservationCollectionCommandHandler(
        IPaymentTransactionRepository transactionRepository,
        IPaymentOrderRepository orderRepository,
        IRefundRequestRepository refundRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReconcileReservationCollectionCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _orderRepository = orderRepository;
        _refundRepository = refundRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ReconcileReservationResponseDto> Handle(
        ReconcileReservationCollectionCommand command,
        CancellationToken cancellationToken)
    {
        var req = command.Request;
        _logger.LogInformation("Processing reconciliation for ReservationId: {ReservationId}, CollectionId: {CollectionId}, Reason: {Reason}",
            req.ReservationId, req.CollectionId, req.Reason);

        var tx = await _transactionRepository.GetByIdAsync(req.CollectionId, cancellationToken);
        if (tx == null)
        {
            _logger.LogWarning("Collection transaction '{CollectionId}' not found.", req.CollectionId);
            throw new PaymentDomainException("TRANSACTION_NOT_FOUND", $"Collection transaction '{req.CollectionId}' not found.", "collectionId");
        }

        var order = await _orderRepository.GetByIdAsync(tx.PaymentOrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("PaymentOrder '{OrderId}' not found.", tx.PaymentOrderId);
            throw new PaymentDomainException(PaymentDomainException.OrderNotFound, $"PaymentOrder '{tx.PaymentOrderId}' not found.", "orderId");
        }

        if (tx.Status != PaymentStatus.SUCCESS)
        {
            _logger.LogWarning("Collection transaction '{CollectionId}' is not settled (Status: {Status}). Cannot reconcile/refund.", req.CollectionId, tx.Status);
            throw new PaymentDomainException("TRANSACTION_NOT_SETTLED", $"Collection transaction '{req.CollectionId}' is not settled (status: {tx.Status}). Only settled transactions can be reconciled/refunded.", "collectionId");
        }

        // Idempotency: check if an identical refund request exists for this order
        var existingRefunds = await _refundRepository.GetByPaymentOrderIdAsync(order.Id, cancellationToken);
        var duplicate = existingRefunds.FirstOrDefault(r => r.Reason == req.Reason);
        if (duplicate != null)
        {
            _logger.LogInformation("Idempotent replay: RefundRequest already exists for OrderId {OrderId}, Reason {Reason}", order.Id, req.Reason);
            return new ReconcileReservationResponseDto(duplicate.Id, req.ReservationId, req.CollectionId, duplicate.Status, duplicate.Reason, duplicate.CreatedAt);
        }

        var refundAmount = req.Amount > 0 ? req.Amount : tx.Amount;
        var now = DateTimeOffset.UtcNow;

        var refund = new RefundRequest
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            PaymentOrderId = order.Id,
            Amount = refundAmount,
            Reason = req.Reason,
            Status = "REFUND_REQUESTED",
            CreatedAt = now,
            UpdatedAt = now
        };

        var refundTx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            PaymentOrderId = order.Id,
            Amount = refundAmount,
            TransactionType = TransactionType.REFUND,
            PaymentMethod = tx.PaymentMethod,
            Status = PaymentStatus.PENDING,
            CreatedAt = now
        };

        await _refundRepository.AddAsync(refund, cancellationToken);
        await _transactionRepository.AddAsync(refundTx, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created RefundRequest '{RefundId}' and REFUND transaction '{RefundTxId}' for Order '{OrderId}'",
            refund.Id, refundTx.Id, order.Id);

        return new ReconcileReservationResponseDto(
            RefundRequestId: refund.Id,
            ReservationId: req.ReservationId,
            CollectionId: req.CollectionId,
            Status: refund.Status,
            Reason: refund.Reason,
            CreatedAt: refund.CreatedAt
        );
    }
}
