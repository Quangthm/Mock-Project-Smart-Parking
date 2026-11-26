using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
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
    private readonly ICurrentActor _currentActor;
    private readonly ILogger<InitiatePaymentCheckoutCommandHandler> _logger;

    public InitiatePaymentCheckoutCommandHandler(
        IPaymentOrderRepository orderRepository,
        IPaymentTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        IPaymentGatewayPort paymentGatewayPort,
        ICurrentActor currentActor,
        ILogger<InitiatePaymentCheckoutCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _paymentGatewayPort = paymentGatewayPort;
        _currentActor = currentActor;
        _logger = logger;
    }

    public async Task<PaymentCheckoutResponseDto> Handle(InitiatePaymentCheckoutCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order == null)
        {
            throw new PaymentDomainException("ORDER_NOT_FOUND", $"Payment order {request.OrderId} was not found.");
        }

        if (order.AccountId.HasValue)
        {
            if (!_currentActor.IsInternalService &&
                (!_currentActor.IsAuthenticated || _currentActor.AccountId != order.AccountId.Value))
            {
                throw new PaymentDomainException(
                    "FORBIDDEN_ORDER_ACCESS",
                    "You are not authorized to initiate checkout for this payment order.");
            }
        }

        if (order.Status == PaymentOrderStatus.PAID)
        {
            throw new PaymentDomainException("ORDER_ALREADY_PAID", "This payment order has already been paid.");
        }

        if (order.IsExpired(now))
        {
            throw new PaymentDomainException("ORDER_EXPIRED", "This payment order has expired.");
        }

        if (order.FinalAmount <= 0)
        {
            throw new PaymentDomainException(
                "ZERO_AMOUNT_ORDER",
                "This order has no amount due, so a PayOS checkout cannot be created.");
        }

        // 1. Idempotency replay by IdempotencyKey if provided
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingTxByKey = await _transactionRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
            if (existingTxByKey != null && existingTxByKey.PaymentOrderId == order.Id)
            {
                if (existingTxByKey.Status == PaymentStatus.PENDING)
                {
                    _logger.LogInformation("Replaying transaction with IdempotencyKey {IdempotencyKey} for OrderId {OrderId}.", request.IdempotencyKey, order.Id);
                    return new PaymentCheckoutResponseDto(
                        PaymentTransactionId: existingTxByKey.Id,
                        OrderCode: long.TryParse(order.OrderCode, out var oc) ? oc : 100000 + (long)Math.Abs(order.Id.GetHashCode() % 900000),
                        Amount: (long)order.FinalAmount,
                        CheckoutUrl: existingTxByKey.CheckoutUrl ?? (string.IsNullOrWhiteSpace(existingTxByKey.GatewayTransactionId) ? "" : $"https://pay.payos.vn/web/{existingTxByKey.GatewayTransactionId}"),
                        QrCode: existingTxByKey.QrCode ?? "",
                        AccountNumber: existingTxByKey.AccountNumber ?? "",
                        AccountName: existingTxByKey.AccountName ?? "",
                        Bin: existingTxByKey.Bin ?? "",
                        Status: existingTxByKey.Status.ToString(),
                        ExpiresAt: order.ExpiresAt ?? now.AddMinutes(15),
                        IsReplayed: true
                    );
                }
            }
        }

        // 2. A pending transaction already represents the active checkout for this order.
        var pendingTx = order.Transactions.FirstOrDefault(t => t.Status == PaymentStatus.PENDING);
        if (pendingTx != null)
        {
            if (IsMockGatewayTransactionId(pendingTx.GatewayTransactionId, pendingTx.Id))
            {
                _logger.LogInformation(
                    "Cancelling stale mock checkout {TransactionId} for OrderId {OrderId} so a live PayOS checkout can be created.",
                    pendingTx.Id,
                    order.Id);
                pendingTx.MarkCancelled();
                _transactionRepository.Update(pendingTx);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(pendingTx.GatewayTransactionId))
                {
                    throw new PaymentDomainException(
                        "PAYOS_CHECKOUT_LINK_UNAVAILABLE",
                        "The pending PayOS checkout has no payment link ID. Check its status or choose cash payment.");
                }

                _logger.LogInformation("Replaying existing pending checkout for OrderId {OrderId}.", order.Id);
                return new PaymentCheckoutResponseDto(
                    PaymentTransactionId: pendingTx.Id,
                    OrderCode: long.TryParse(order.OrderCode, out var oc) ? oc : 100000 + (long)Math.Abs(order.Id.GetHashCode() % 900000),
                    Amount: (long)order.FinalAmount,
                    CheckoutUrl: pendingTx.CheckoutUrl ?? $"https://pay.payos.vn/web/{pendingTx.GatewayTransactionId}",
                    QrCode: pendingTx.QrCode ?? "",
                    AccountNumber: pendingTx.AccountNumber ?? "",
                    AccountName: pendingTx.AccountName ?? "",
                    Bin: pendingTx.Bin ?? "",
                    Status: pendingTx.Status.ToString(),
                    ExpiresAt: order.ExpiresAt ?? now.AddMinutes(15),
                    IsReplayed: true
                );
            }
        }

        // 3. Block checkout retry if there is an unresolved UNKNOWN transaction
        var hasAmbiguousTx = order.Transactions.Any(t => t.Status == PaymentStatus.UNKNOWN);
        if (hasAmbiguousTx)
        {
            throw new PaymentDomainException(
                "PAYMENT_TRANSACTION_AMBIGUOUS",
                "There is an unresolved transaction in an unknown state with the payment provider. Please verify transaction status before attempting again.");
        }

        // 4. Check retry limit
        var failedAttempts = order.Transactions.Count(t => t.Status == PaymentStatus.FAILED);
        if (!PaymentStateMachine.IsRetryAllowed(failedAttempts))
        {
            throw new PaymentDomainException("PAYMENT_RETRY_LIMIT_EXCEEDED", $"The payment retry limit ({PaymentStateMachine.MaxRetryLimit}) has been reached.");
        }

        var transactionId = Guid.NewGuid();
        long orderCodeNumber;
        if (!long.TryParse(order.OrderCode, out orderCodeNumber))
        {
            orderCodeNumber = Math.Abs(BitConverter.ToInt64(order.Id.ToByteArray(), 0)) % 900000000000L + 100000000000L;
        }

        var expiresAt = order.ExpiresAt ?? now.AddMinutes(15);
        var description = $"SP {orderCodeNumber}";

        // 5. Persist attempt intent with PENDING status BEFORE calling external gateway!
        var transaction = new PaymentTransaction
        {
            Id = transactionId,
            TenantId = order.TenantId,
            PaymentOrderId = order.Id,
            TransactionType = TransactionType.PAYMENT,
            PaymentMethod = request.PaymentMethod,
            IdempotencyKey = request.IdempotencyKey,
            Amount = order.FinalAmount,
            Status = PaymentStatus.PENDING,
            CreatedAt = now
        };
        await _transactionRepository.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
        if (gatewayResult.IsSuccess && IsMockGatewayTransactionId(gatewayResult.GatewayTransactionId, transactionId))
        {
            transaction.MarkCancelled();
            _transactionRepository.Update(transaction);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw new PaymentDomainException(
                "PAYOS_CREDENTIALS_NOT_CONFIGURED",
                "Live PayOS credentials are not loaded by Payment Service. The demo link was blocked; configure the credentials and retry.");
        }

        if (!gatewayResult.IsSuccess)
        {
            if (gatewayResult.ErrorCode == "GATEWAY_TIMEOUT")
            {
                transaction.MarkUnknown();
            }
            else
            {
                transaction.MarkFailed();
            }

            _transactionRepository.Update(transaction);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw new PaymentDomainException(
                gatewayResult.ErrorCode ?? "GATEWAY_INITIATION_FAILED",
                gatewayResult.ErrorMessage ?? "Could not connect to the PayOS payment gateway.");
        }

        // 6. Record authoritative provider data onto the transaction
        transaction.GatewayTransactionId = gatewayResult.GatewayTransactionId;
        transaction.CheckoutUrl = gatewayResult.CheckoutUrl;
        transaction.QrCode = gatewayResult.QrCode;
        transaction.AccountNumber = gatewayResult.AccountNumber;
        transaction.AccountName = gatewayResult.AccountName;
        transaction.Bin = gatewayResult.Bin;

        _transactionRepository.Update(transaction);
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

    private static bool IsMockGatewayTransactionId(string? gatewayTransactionId, Guid transactionId) =>
        gatewayTransactionId?.StartsWith("MOCK-", StringComparison.Ordinal) == true ||
        (gatewayTransactionId is not null &&
         Guid.TryParseExact(gatewayTransactionId, "N", out var parsedId) &&
         parsedId == transactionId);
}
