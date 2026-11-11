using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Common.Mappings;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Events;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.CollectCashPayment;

public sealed class CollectCashPaymentCommandHandler : IRequestHandler<CollectCashPaymentCommand, CustomerInvoiceResponseDto>
{
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentActor _currentActor;
    private readonly IPaymentGatewayPort _paymentGateway;
    private readonly ILogger<CollectCashPaymentCommandHandler> _logger;

    public CollectCashPaymentCommandHandler(
        IPaymentOrderRepository orderRepository,
        IPaymentTransactionRepository transactionRepository,
        IInvoiceRepository invoiceRepository,
        IUnitOfWork unitOfWork,
        IEventPublisher eventPublisher,
        ICurrentActor currentActor,
        IPaymentGatewayPort paymentGateway,
        ILogger<CollectCashPaymentCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _transactionRepository = transactionRepository;
        _invoiceRepository = invoiceRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
        _currentActor = currentActor;
        _paymentGateway = paymentGateway;
        _logger = logger;
    }

    public async Task<CustomerInvoiceResponseDto> Handle(CollectCashPaymentCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Payment order {OrderId} not found for cash collection.", request.OrderId);
            throw new PaymentDomainException(PaymentDomainException.OrderNotFound, $"Payment order {request.OrderId} was not found.");
        }

        // Authentication & Role-based Authorization check: Only OPERATOR or ADMIN or S2S
        if (!_currentActor.IsAuthenticated)
        {
            throw new PaymentDomainException(PaymentDomainException.Unauthorized, "The authentication token is missing or invalid.");
        }

        if (!_currentActor.IsInternalService)
        {
            if (string.Equals(_currentActor.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
            {
                if (_currentActor.TenantId.HasValue && _currentActor.TenantId.Value != Guid.Empty && _currentActor.TenantId.Value != order.TenantId)
                {
                    throw new PaymentDomainException(
                        PaymentDomainException.CrossTenantAccessDenied,
                        "An administrator cannot collect cash for an order belonging to another tenant.");
                }
            }
            else if (string.Equals(_currentActor.Role, "OPERATOR", StringComparison.OrdinalIgnoreCase))
            {
                if (!_currentActor.SiteId.HasValue || _currentActor.SiteId.Value == Guid.Empty || _currentActor.SiteId.Value != order.SiteId)
                {
                    throw new PaymentDomainException(
                        PaymentDomainException.OperatorSiteMismatch,
                        "An operator cannot collect cash outside their assigned parking site.");
                }
            }
            else
            {
                throw new PaymentDomainException(
                    PaymentDomainException.ForbiddenInvoiceAccess,
                    "Only parking operators and administrators can collect cash payments.");
            }
        }

        var invoice = await _invoiceRepository.GetByPaymentOrderIdAsync(order.Id, cancellationToken);

        // Idempotency: If already paid, replay response without duplicating outbox event
        if (order.Status == PaymentOrderStatus.PAID)
        {
            _logger.LogInformation("Order {OrderId} is already PAID. Replaying invoice for cash collection request.", order.Id);
            if (invoice != null)
            {
                // A previous success event may have been published while the downstream
                // consumer was unavailable. Re-stage it on an idempotent cash replay so
                // Operations can finish the session without creating another transaction.
                var successfulCashTransaction = order.Transactions.FirstOrDefault(
                    transaction => transaction.Status == PaymentStatus.SUCCESS &&
                                   transaction.PaymentMethod == PaymentMethod.CASH);
                if (successfulCashTransaction is not null)
                {
                    await PublishCashPaymentSucceededAsync(
                        order, invoice, successfulCashTransaction, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    return InvoiceDtoMapper.MapToDto(invoice, order);
                }

                throw new PaymentDomainException(
                    "ORDER_ALREADY_PAID_ONLINE",
                    "This order has already been paid online. Cash was not collected.");
            }
        }

        if (order.Status == PaymentOrderStatus.CANCELLED || order.Status == PaymentOrderStatus.FAILED)
        {
            _logger.LogWarning("Cannot collect cash for order {OrderId} in status {Status}.", order.Id, order.Status);
            throw new PaymentDomainException("ORDER_INVALID_STATUS", $"Cash cannot be collected while the order is {order.Status}.");
        }

        if (invoice == null)
        {
            _logger.LogWarning("Invoice for order {OrderId} not found before cash collection.", order.Id);
            throw new PaymentDomainException(PaymentDomainException.InvoiceNotFound, "No invoice was found for this payment order.");
        }

        foreach (var pendingPayOsTransaction in order.Transactions.Where(
                     transaction => transaction.Status == PaymentStatus.PENDING &&
                                    transaction.PaymentMethod == PaymentMethod.PAYOS))
        {
            if (!long.TryParse(order.OrderCode, out var orderCode))
            {
                throw new PaymentDomainException("INVALID_ORDER_CODE", "The pending PayOS checkout cannot be safely cancelled.");
            }

            var cancellation = await _paymentGateway.CancelPaymentAsync(
                orderCode,
                pendingPayOsTransaction.Id,
                pendingPayOsTransaction.GatewayTransactionId,
                cancellationToken);

            if (cancellation.PaymentStatus == "PAID")
            {
                throw new PaymentDomainException(
                    "ORDER_ALREADY_PAID_ONLINE",
                    "PayOS has already completed this payment. Cash was not collected.");
            }

            if (!cancellation.IsSuccess || cancellation.PaymentStatus != "CANCELLED")
            {
                _logger.LogWarning(
                    "Could not cancel pending PayOS transaction {TransactionId} for cash switch: {Status} {ErrorCode}",
                    pendingPayOsTransaction.Id,
                    cancellation.PaymentStatus,
                    cancellation.ErrorCode);
                throw new PaymentDomainException(
                    "PAYOS_CHECKOUT_CANCELLATION_FAILED",
                    "The pending PayOS checkout could not be cancelled. Cash was not collected; check the PayOS status and try again.");
            }

            pendingPayOsTransaction.MarkCancelled();
            _transactionRepository.Update(pendingPayOsTransaction);
        }

        var transactionId = Guid.NewGuid();
        var gatewayTxId = $"CASH-{order.OrderCode}";

        var transaction = new PaymentTransaction
        {
            Id = transactionId,
            TenantId = order.TenantId,
            PaymentOrderId = order.Id,
            TransactionType = TransactionType.PAYMENT,
            PaymentMethod = PaymentMethod.CASH,
            GatewayTransactionId = gatewayTxId,
            Amount = order.FinalAmount,
            Status = PaymentStatus.SUCCESS,
            PaidAt = now,
            CreatedAt = now
        };

        order.MarkPaid(now);

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        _orderRepository.Update(order);

        invoice.PricingSnapshot.Status = "PAID";
        invoice.PricingSnapshot.IsSettled = true;
        invoice.PricingSnapshot.PaidAt = now;
        invoice.PricingSnapshot.PaymentMethod = "CASH";
        invoice.PricingSnapshot.TransactionReference = transaction.Id.ToString();
        invoice.PricingSnapshot.GatewayTransactionId = gatewayTxId;
        _invoiceRepository.Update(invoice);

        // Stage PaymentSucceededV1 before SaveChangesAsync so it commits with the payment.
        await PublishCashPaymentSucceededAsync(order, invoice, transaction, cancellationToken);

        // Atomic DB Commit: order + transaction + invoice + outboxMessage in one transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cash payment recorded and PaymentSucceededV1 staged into outbox for Order {OrderId}, Tx {TxId}.", order.Id, transaction.Id);

        return InvoiceDtoMapper.MapToDto(invoice, order);
    }

    private Task PublishCashPaymentSucceededAsync(
        PaymentOrder order,
        Invoice invoice,
        PaymentTransaction transaction,
        CancellationToken cancellationToken)
    {
        var snapshot = invoice.PricingSnapshot;
        return _eventPublisher.PublishAsync(new PaymentSucceededV1
        {
            EventId = Guid.NewGuid(),
            OccurredAt = transaction.PaidAt ?? order.UpdatedAt,
            CorrelationId = order.OrderCode,
            PaymentOrderId = order.Id,
            PaymentTransactionId = transaction.Id,
            PaymentMethod = PaymentMethod.CASH.ToString(),
            GatewayReference = transaction.GatewayTransactionId,
            TenantId = order.TenantId,
            SiteId = order.SiteId,
            ReferenceType = order.ReferenceType.ToString(),
            ReferenceId = order.ReferenceId,
            PlateNumber = snapshot.PlateNumber,
            AccountId = order.AccountId,
            Amount = order.FinalAmount,
            ParkingFee = invoice.BaseParkingFee > 0 ? invoice.BaseParkingFee : order.FinalAmount,
            PenaltyFee = invoice.OverstayPenaltyFee,
            PenaltyReason = snapshot.LineItems.FirstOrDefault(line => line.ItemType == "PENALTY_OVERSTAY")?.Description,
            Currency = snapshot.Currency ?? "VND"
        }, cancellationToken);
    }
}
