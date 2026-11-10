using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Domain.Services;

namespace SmartParking.PaymentService.Application.Usecase.InitiateCheckout;

public class InitiatePaymentCheckoutCommandHandler : IRequestHandler<InitiatePaymentCheckoutCommand, PaymentCheckoutResponseDto>
{
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGatewayPort _paymentGatewayPort;
    private readonly ILogger<InitiatePaymentCheckoutCommandHandler> _logger;

    public InitiatePaymentCheckoutCommandHandler(
        IPaymentOrderRepository orderRepository,
        IPaymentTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        IPaymentGatewayPort paymentGatewayPort,
        ILogger<InitiatePaymentCheckoutCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _paymentGatewayPort = paymentGatewayPort;
        _logger = logger;
    }

    public async Task<PaymentCheckoutResponseDto> Handle(InitiatePaymentCheckoutCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order == null)
        {
            throw new PaymentDomainException("ORDER_NOT_FOUND", $"Không tìm thấy đơn hàng thanh toán {request.OrderId}.");
        }

        if (order.Status == PaymentOrderStatus.PAID)
        {
            throw new PaymentDomainException("ORDER_ALREADY_PAID", "Đơn hàng này đã được thanh toán thành công trước đó.");
        }

        if (order.IsExpired(now))
        {
            throw new PaymentDomainException("ORDER_EXPIRED", "This payment order has expired.");
        }

        if (order.FinalAmount <= 0)
        {
            throw new PaymentDomainException(
                "ZERO_AMOUNT_ORDER",
                "Đơn hàng không phát sinh phí; không cần tạo thanh toán PayOS.");
        }

        // A pending transaction already represents the active checkout for this order.
        // PaymentOrder.IdempotencyKey belongs to order preparation and is globally unique;
        // do not replace it with the separate checkout request's idempotency key.
        var pendingTx = order.Transactions.FirstOrDefault(t => t.Status == PaymentStatus.PENDING);
        if (pendingTx != null)
        {
            _logger.LogInformation("Replaying existing pending checkout for OrderId {OrderId}.", order.Id);
            return new PaymentCheckoutResponseDto(
                PaymentTransactionId: pendingTx.Id,
                OrderCode: long.TryParse(order.OrderCode, out var oc) ? oc : 100000 + (long)Math.Abs(order.Id.GetHashCode() % 900000),
                Amount: (long)order.FinalAmount,
                CheckoutUrl: IsMockGatewayTransactionId(pendingTx.GatewayTransactionId)
                    ? $"http://localhost:5173/payment/success?transactionId={pendingTx.Id}"
                    : $"https://pay.payos.vn/web/{pendingTx.GatewayTransactionId ?? pendingTx.Id.ToString("N")}",
                QrCode: "00020101021238540010A00000072701240006970422011001234567890208QRIBFTTA5303704540250005802VN62230819Thanh toan do xe 20266304ABCD",
                AccountNumber: "0123456789",
                AccountName: "SMARTPARKING CORP",
                Bin: "970422",
                Status: pendingTx.Status.ToString(),
                ExpiresAt: order.ExpiresAt ?? now.AddMinutes(15),
                IsReplayed: true
            );
        }

        // Check retry limit
        var failedAttempts = order.Transactions.Count(t => t.Status == PaymentStatus.FAILED);
        if (!PaymentStateMachine.IsRetryAllowed(failedAttempts))
        {
            throw new PaymentDomainException("PAYMENT_RETRY_LIMIT_EXCEEDED", $"Đơn hàng đã vượt quá giới hạn thử lại ({PaymentStateMachine.MaxRetryLimit} lần).");
        }

        var transactionId = Guid.NewGuid();
        long orderCodeNumber;
        if (!long.TryParse(order.OrderCode, out orderCodeNumber))
        {
            orderCodeNumber = Math.Abs(BitConverter.ToInt64(order.Id.ToByteArray(), 0)) % 900000000000L + 100000000000L;
        }

        var expiresAt = order.ExpiresAt ?? now.AddMinutes(15);
        var description = $"SP {orderCodeNumber}";

        var gatewayRequest = new PaymentInitiateRequest(
            TransactionId: transactionId,
            OrderCode: orderCodeNumber,
            AmountVnd: (long)order.FinalAmount,
            Description: description,
            ReturnUrl: request.ReturnUrl ?? "https://smartparking.vn/driver/checkout/success",
            CancelUrl: request.CancelUrl ?? "https://smartparking.vn/driver/checkout/cancel",
            ExpiresAt: expiresAt
        );

        var gatewayResult = await _paymentGatewayPort.InitiatePaymentAsync(gatewayRequest, cancellationToken);
        if (!gatewayResult.IsSuccess)
        {
            var failedTx = new PaymentTransaction
            {
                Id = transactionId,
                TenantId = order.TenantId,
                PaymentOrderId = order.Id,
                TransactionType = TransactionType.PAYMENT,
                PaymentMethod = request.PaymentMethod,
                Amount = order.FinalAmount,
                Status = gatewayResult.ErrorCode == "GATEWAY_TIMEOUT" ? PaymentStatus.UNKNOWN : PaymentStatus.FAILED,
                CreatedAt = now
            };
            await _transactionRepository.AddAsync(failedTx, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw new PaymentDomainException(
                gatewayResult.ErrorCode ?? "GATEWAY_INITIATION_FAILED",
                gatewayResult.ErrorMessage ?? "Không thể kết nối đến cổng thanh toán payOS.");
        }

        var transaction = new PaymentTransaction
        {
            Id = transactionId,
            TenantId = order.TenantId,
            PaymentOrderId = order.Id,
            TransactionType = TransactionType.PAYMENT,
            PaymentMethod = request.PaymentMethod,
            GatewayTransactionId = gatewayResult.GatewayTransactionId,
            Amount = order.FinalAmount,
            Status = PaymentStatus.PENDING,
            CreatedAt = now
        };

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PaymentCheckoutResponseDto(
            PaymentTransactionId: transaction.Id,
            OrderCode: orderCodeNumber,
            Amount: (long)order.FinalAmount,
            CheckoutUrl: gatewayResult.CheckoutUrl ?? "",
            QrCode: gatewayResult.QrCode ?? "",
            AccountNumber: gatewayResult.AccountNumber ?? "",
            AccountName: gatewayResult.AccountName ?? "",
            Bin: gatewayResult.Bin ?? "",
            Status: transaction.Status.ToString(),
            ExpiresAt: expiresAt,
            IsReplayed: false
        );
    }

    private static bool IsMockGatewayTransactionId(string? gatewayTransactionId) =>
        gatewayTransactionId?.StartsWith("MOCK-", StringComparison.Ordinal) == true ||
        (gatewayTransactionId is not null &&
         Guid.TryParseExact(gatewayTransactionId, "N", out _));
}
