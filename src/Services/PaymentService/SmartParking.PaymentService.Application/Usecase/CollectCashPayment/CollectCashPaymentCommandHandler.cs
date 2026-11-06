using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
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
    private readonly ILogger<CollectCashPaymentCommandHandler> _logger;

    public CollectCashPaymentCommandHandler(
        IPaymentOrderRepository orderRepository,
        IPaymentTransactionRepository transactionRepository,
        IInvoiceRepository invoiceRepository,
        IUnitOfWork unitOfWork,
        IEventPublisher eventPublisher,
        ICurrentActor currentActor,
        ILogger<CollectCashPaymentCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _transactionRepository = transactionRepository;
        _invoiceRepository = invoiceRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
        _currentActor = currentActor;
        _logger = logger;
    }

    public async Task<CustomerInvoiceResponseDto> Handle(CollectCashPaymentCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Payment order {OrderId} not found for cash collection.", request.OrderId);
            throw new PaymentDomainException(PaymentDomainException.OrderNotFound, $"Không tìm thấy đơn hàng thanh toán {request.OrderId}.");
        }

        // Authentication & Role-based Authorization check: Only OPERATOR or ADMIN or S2S
        if (!_currentActor.IsAuthenticated)
        {
            throw new PaymentDomainException(PaymentDomainException.Unauthorized, "Thiếu token xác thực hoặc token không hợp lệ.");
        }

        if (!_currentActor.IsInternalService)
        {
            if (string.Equals(_currentActor.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
            {
                if (_currentActor.TenantId.HasValue && _currentActor.TenantId.Value != Guid.Empty && _currentActor.TenantId.Value != order.TenantId)
                {
                    throw new PaymentDomainException(
                        PaymentDomainException.CrossTenantAccessDenied,
                        "Quản trị viên không có quyền thu tiền mặt cho đơn hàng của tenant khác.");
                }
            }
            else if (string.Equals(_currentActor.Role, "OPERATOR", StringComparison.OrdinalIgnoreCase))
            {
                if (!_currentActor.SiteId.HasValue || _currentActor.SiteId.Value == Guid.Empty || _currentActor.SiteId.Value != order.SiteId)
                {
                    throw new PaymentDomainException(
                        PaymentDomainException.OperatorSiteMismatch,
                        "Nhân viên cổng không có quyền thu tiền mặt ngoài bãi trực được phân công.");
                }
            }
            else
            {
                throw new PaymentDomainException(
                    PaymentDomainException.ForbiddenInvoiceAccess,
                    "Chỉ nhân viên vận hành bốt trực (OPERATOR) hoặc quản trị viên (ADMIN) mới có quyền thu tiền mặt.");
            }
        }

        var invoice = await _invoiceRepository.GetByPaymentOrderIdAsync(order.Id, cancellationToken);

        // Idempotency: If already paid, replay response without duplicating outbox event
        if (order.Status == PaymentOrderStatus.PAID)
        {
            _logger.LogInformation("Order {OrderId} is already PAID. Replaying invoice for cash collection request.", order.Id);
            if (invoice != null)
            {
                return InvoiceDtoMapper.MapToDto(invoice, order);
            }
        }

        if (order.Status == PaymentOrderStatus.CANCELLED || order.Status == PaymentOrderStatus.FAILED)
        {
            _logger.LogWarning("Cannot collect cash for order {OrderId} in status {Status}.", order.Id, order.Status);
            throw new PaymentDomainException("ORDER_INVALID_STATUS", $"Đơn hàng ở trạng thái {order.Status} không thể thu tiền mặt.");
        }

        if (invoice == null)
        {
            _logger.LogWarning("Invoice for order {OrderId} not found before cash collection.", order.Id);
            throw new PaymentDomainException(PaymentDomainException.InvoiceNotFound, "Không tìm thấy hóa đơn tài chính tương ứng với đơn hàng.");
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

        // Stage Outbox Event PaymentSucceededV1 BEFORE SaveChangesAsync to guarantee atomic commit!
        var outboxEvent = new PaymentSucceededV1
        {
            EventId = Guid.NewGuid(),
            OccurredAt = now,
            CorrelationId = order.OrderCode,
            PaymentOrderId = order.Id,
            PaymentTransactionId = transaction.Id,
            PaymentMethod = "CASH",
            GatewayReference = gatewayTxId,
            TenantId = order.TenantId,
            SiteId = order.SiteId,
            ReferenceType = order.ReferenceType.ToString(),
            ReferenceId = order.ReferenceId,
            PlateNumber = invoice.PricingSnapshot.PlateNumber,
            AccountId = order.AccountId,
            Amount = order.FinalAmount,
            ParkingFee = invoice.BaseParkingFee > 0 ? invoice.BaseParkingFee : order.FinalAmount,
            PenaltyFee = invoice.OverstayPenaltyFee,
            PenaltyReason = invoice.PricingSnapshot.LineItems.FirstOrDefault(l => l.ItemType == "PENALTY_OVERSTAY")?.Description,
            Currency = invoice.PricingSnapshot.Currency ?? "VND"
        };

        await _eventPublisher.PublishAsync(outboxEvent, cancellationToken);

        // Atomic DB Commit: order + transaction + invoice + outboxMessage in one transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cash payment recorded and PaymentSucceededV1 staged into outbox for Order {OrderId}, Tx {TxId}.", order.Id, transaction.Id);

        return InvoiceDtoMapper.MapToDto(invoice, order);
    }
}
