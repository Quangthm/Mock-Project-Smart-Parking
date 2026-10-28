using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Ports;

namespace SmartParking.PaymentService.Application.Usecase.GetPaymentStatus;

public class GetPaymentStatusQueryHandler : IRequestHandler<GetPaymentStatusQuery, PaymentTransactionStatusDto>
{
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGatewayPort _gatewayPort;
    private readonly ILogger<GetPaymentStatusQueryHandler> _logger;

    public GetPaymentStatusQueryHandler(
        IPaymentTransactionRepository transactionRepository,
        IPaymentOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IPaymentGatewayPort gatewayPort,
        ILogger<GetPaymentStatusQueryHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _gatewayPort = gatewayPort;
        _logger = logger;
    }

    public async Task<PaymentTransactionStatusDto> Handle(GetPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        var tx = await _transactionRepository.GetByIdAsync(request.TransactionId, cancellationToken);
        if (tx == null)
        {
            throw new PaymentDomainException("TRANSACTION_NOT_FOUND", $"Không tìm thấy giao dịch thanh toán {request.TransactionId}.");
        }

        var order = tx.PaymentOrder;
        long orderCodeNumber = 0;
        if (order != null && long.TryParse(order.OrderCode, out var oc))
        {
            orderCodeNumber = oc;
        }

        // Authoritative state check:
        // If already in definitive terminal state, return immediately from database
        if (tx.Status is PaymentStatus.SUCCESS or PaymentStatus.CANCELLED)
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
            var reconcile = await _gatewayPort.QueryTransactionAsync(orderCodeNumber, tx.GatewayTransactionId, cancellationToken);
            if (reconcile.IsFound)
            {
                if (reconcile.PaymentStatus == "SUCCESS")
                {
                    var paidAt = reconcile.PaidAt ?? DateTimeOffset.UtcNow;
                    tx.MarkSuccess(reconcile.GatewayTransactionId, paidAt);
                    order?.MarkPaid(paidAt);

                    _transactionRepository.Update(tx);
                    if (order != null) _orderRepository.Update(order);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Reconciled transaction {TxId} to SUCCESS with payOS.", tx.Id);
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
}
