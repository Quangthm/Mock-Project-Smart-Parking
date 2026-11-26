using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Events;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Reservations;

namespace SmartParking.PaymentService.Application.Usecase.GetPaymentStatus;

public class GetPaymentStatusQueryHandler : IRequestHandler<GetPaymentStatusQuery, PaymentTransactionStatusDto>
{
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGatewayPort _gatewayPort;
    private readonly ILogger<GetPaymentStatusQueryHandler> _logger;
    private readonly ReviewedPayOsSettlement _reviewedSettlement;

    public GetPaymentStatusQueryHandler(
        IPaymentTransactionRepository transactionRepository,
        IPaymentOrderRepository orderRepository,
        IInvoiceRepository invoiceRepository,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        IPaymentGatewayPort gatewayPort,
        ILogger<GetPaymentStatusQueryHandler> logger,
        ReviewedPayOsSettlement reviewedSettlement)
    {
        _transactionRepository = transactionRepository;
        _orderRepository = orderRepository;
        _invoiceRepository = invoiceRepository;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _gatewayPort = gatewayPort;
        _logger = logger;
        _reviewedSettlement = reviewedSettlement;
    }

    public async Task<PaymentTransactionStatusDto> Handle(GetPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        var tx = request.EnableReviewedVerification
            ? await _transactionRepository.GetStatusSnapshotAsync(request.TransactionId, cancellationToken)
            : await _transactionRepository.GetByIdAsync(request.TransactionId, cancellationToken);
        if (tx == null)
        {
            throw new PaymentDomainException("TRANSACTION_NOT_FOUND", $"Payment transaction {request.TransactionId} was not found.");
        }

        var order = tx.PaymentOrder ?? (tx.PaymentOrderId != Guid.Empty ? await _orderRepository.GetByIdAsync(tx.PaymentOrderId, cancellationToken) : null);
        long orderCodeNumber = 0;
        if (order != null && long.TryParse(order.OrderCode, out var oc))
        {
            orderCodeNumber = oc;
        }

        if (order?.ReviewedMethod is not null)
            return await ReadReviewedStatusAsync(request, tx, order, orderCodeNumber, cancellationToken);

        // Authoritative state check:
        // If already in definitive terminal state, return immediately from database
        if (tx.Status is PaymentStatus.SUCCESS or PaymentStatus.CANCELLED ||
            tx.PaymentMethod == PaymentMethod.CASH)
        {
            return new PaymentTransactionStatusDto(
                TransactionId: tx.Id,
                OrderId: tx.PaymentOrderId,
                OrderCode: orderCodeNumber,
                Amount: (long)tx.Amount,
                PaymentMethod: tx.PaymentMethod.ToString(),
                GatewayTransactionId: tx.GatewayTransactionId,
                Status: tx.Status.ToString(),
                PaidAt: tx.PaidAt,
                IsAuthoritative: true
            );
        }

        // Active reconciliation with payOS when in non-definitive state (PENDING / UNKNOWN)
        if (orderCodeNumber > 0)
        {
            if (request.EnableReviewedVerification)
            {
                tx = await _transactionRepository.GetByIdAsync(request.TransactionId, cancellationToken)
                    ?? throw TransactionNotFound(request.TransactionId);
                order = tx.PaymentOrder;
                if (tx.Status is PaymentStatus.SUCCESS or PaymentStatus.CANCELLED)
                    return StatusOf(tx, orderCodeNumber, authoritative: true);
            }

            var reconcile = await _gatewayPort.QueryTransactionAsync(orderCodeNumber, tx.GatewayTransactionId, cancellationToken);
            if (reconcile.IsFound)
            {
                if (reconcile.PaymentStatus == "SUCCESS")
                {
                    var paidAt = reconcile.PaidAt ?? DateTimeOffset.UtcNow;
                    var gatewayTxId = reconcile.GatewayTransactionId ?? tx.GatewayTransactionId;
                    tx.MarkSuccess(gatewayTxId, paidAt);
                    _transactionRepository.Update(tx);

                    if (order != null)
                    {
                        var alreadyPaid = order.Status == PaymentOrderStatus.PAID;
                        if (!alreadyPaid)
                        {
                            order.MarkPaid(paidAt);
                            _orderRepository.Update(order);

                            var invoice = await _invoiceRepository.GetByPaymentOrderIdAsync(order.Id, cancellationToken);
                            if (invoice != null)
                            {
                                invoice.PricingSnapshot.Status = "PAID";
                                invoice.PricingSnapshot.IsSettled = true;
                                invoice.PricingSnapshot.PaidAt = paidAt;
                                invoice.PricingSnapshot.PaymentMethod = tx.PaymentMethod.ToString();
                                invoice.PricingSnapshot.TransactionReference = tx.Id.ToString();
                                invoice.PricingSnapshot.GatewayTransactionId = gatewayTxId;
                                _invoiceRepository.Update(invoice);
                            }

                            var outboxEvent = new PaymentSucceededV1
                            {
                                EventId = Guid.NewGuid(),
                                OccurredAt = paidAt,
                                CorrelationId = order.OrderCode,
                                PaymentOrderId = order.Id,
                                PaymentTransactionId = tx.Id,
                                PaymentMethod = tx.PaymentMethod.ToString(),
                                GatewayReference = gatewayTxId,
                                TenantId = order.TenantId,
                                SiteId = order.SiteId,
                                ReferenceType = order.ReferenceType.ToString(),
                                ReferenceId = order.ReferenceId,
                                PlateNumber = invoice?.PricingSnapshot.PlateNumber,
                                AccountId = order.AccountId,
                                Amount = order.FinalAmount,
                                ParkingFee = invoice?.BaseParkingFee ?? order.FinalAmount,
                                PenaltyFee = invoice?.OverstayPenaltyFee,
                                PenaltyReason = invoice?.PricingSnapshot.LineItems.FirstOrDefault(l => l.ItemType == "PENALTY_OVERSTAY")?.Description,
                                Currency = invoice?.PricingSnapshot.Currency ?? "VND"
                            };

                            await _eventPublisher.PublishAsync(outboxEvent, cancellationToken);
                        }
                    }

                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Reconciled transaction {TxId} to SUCCESS with payOS, settled invoice, and staged outbox event.", tx.Id);
                }
                else if (reconcile.PaymentStatus == "CANCELLED")
                {
                    tx.MarkCancelled();
                    _transactionRepository.Update(tx);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Reconciled transaction {TxId} to CANCELLED with payOS.", tx.Id);
                }
            }
        }

        return new PaymentTransactionStatusDto(
            TransactionId: tx.Id,
            OrderId: tx.PaymentOrderId,
            OrderCode: orderCodeNumber,
            Amount: (long)tx.Amount,
            PaymentMethod: tx.PaymentMethod.ToString(),
            GatewayTransactionId: tx.GatewayTransactionId,
            Status: tx.Status.ToString(),
            PaidAt: tx.PaidAt,
            IsAuthoritative: tx.Status == PaymentStatus.SUCCESS
        );
    }

    private async Task<PaymentTransactionStatusDto> ReadReviewedStatusAsync(GetPaymentStatusQuery request,
        PaymentTransaction tx, PaymentOrder order, long orderCode, CancellationToken ct)
    {
        if (request.AccountId == Guid.Empty || request.AccountId != order.AccountId)
            throw TransactionNotFound(tx.Id);

        // Temporary Development-only fallback while this channel has no working webhook destination.
        if (request.EnableReviewedVerification && orderCode > 0 &&
            order.ReferenceType == ReferenceType.RESERVATION && tx.TenantId == order.TenantId &&
            order.ReviewedMethod == nameof(PaymentMethod.PAYOS) && order.ReviewedCurrency == "VND" &&
            tx.PaymentMethod == PaymentMethod.PAYOS && tx.TransactionType == TransactionType.PAYMENT &&
            tx.Status is PaymentStatus.PENDING or PaymentStatus.UNKNOWN &&
            !string.IsNullOrWhiteSpace(tx.GatewayTransactionId) && !string.IsNullOrWhiteSpace(tx.QrCode))
        {
            _logger.LogInformation("Active PayOS status verification requested. OrderCode {OrderCode}, PaymentOrderId {PaymentOrderId}, PaymentTransactionId {PaymentTransactionId}",
                orderCode, order.Id, tx.Id);
            var result = await _gatewayPort.QueryTransactionAsync(orderCode, tx.GatewayTransactionId, ct, tx.QrCode);
            if (result.ReviewedTransfers is { Count: > 0 } transfers)
                await _reviewedSettlement.RecordTransfersAsync(order.Id, transfers, ct);

            tx = await _transactionRepository.GetStatusSnapshotAsync(tx.Id, ct) ?? throw TransactionNotFound(tx.Id);
        }

        var boundOrder = tx.PaymentOrder ?? order;
        var verifiedSettlement = boundOrder.Status == PaymentOrderStatus.PAID && tx.Amount == boundOrder.FinalAmount &&
            !string.IsNullOrWhiteSpace(tx.GatewayTransactionId) && tx.HasVerifiedProviderCollection();
        return StatusOf(tx, orderCode, authoritative: verifiedSettlement || tx.Status == PaymentStatus.CANCELLED);
    }

    private static PaymentDomainException TransactionNotFound(Guid id) =>
        new("TRANSACTION_NOT_FOUND", $"Payment transaction {id} was not found.");

    private static PaymentTransactionStatusDto StatusOf(PaymentTransaction tx, long orderCode, bool authoritative) =>
        new(tx.Id, tx.PaymentOrderId, orderCode, (long)tx.Amount, tx.PaymentMethod.ToString(),
            tx.GatewayTransactionId, tx.Status.ToString(), tx.PaidAt, authoritative);
}
