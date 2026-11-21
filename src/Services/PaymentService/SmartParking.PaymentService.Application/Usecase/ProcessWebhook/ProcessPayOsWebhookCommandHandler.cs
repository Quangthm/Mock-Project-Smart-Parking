using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Events;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;

namespace SmartParking.PaymentService.Application.Usecase.ProcessWebhook;

public class ProcessPayOsWebhookCommandHandler : IRequestHandler<ProcessPayOsWebhookCommand, ProcessPayOsWebhookResult>
{
    private readonly IPaymentGatewayPort _gatewayPort;
    private readonly IProviderWebhookRepository _webhookRepository;
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IRefundRequestRepository _refundRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessPayOsWebhookCommandHandler> _logger;

    public ProcessPayOsWebhookCommandHandler(
        IPaymentGatewayPort gatewayPort,
        IProviderWebhookRepository webhookRepository,
        IPaymentOrderRepository orderRepository,
        IPaymentTransactionRepository transactionRepository,
        IInvoiceRepository invoiceRepository,
        IRefundRequestRepository refundRepository,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        ILogger<ProcessPayOsWebhookCommandHandler> logger)
    {
        _gatewayPort = gatewayPort;
        _webhookRepository = webhookRepository;
        _orderRepository = orderRepository;
        _transactionRepository = transactionRepository;
        _invoiceRepository = invoiceRepository;
        _refundRepository = refundRepository;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ProcessPayOsWebhookResult> Handle(ProcessPayOsWebhookCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify and parse webhook payload
        var parseResult = await _gatewayPort.VerifyAndParseWebhookAsync(request.RawPayload, request.Signature, cancellationToken);
        if (!parseResult.IsValidSignature)
        {
            _logger.LogWarning("Webhook signature verification failed for payOS callback.");
            return new ProcessPayOsWebhookResult(false, "The webhook signature is invalid.", "INVALID_WEBHOOK_SIGNATURE");
        }

        var reviewedId = await _orderRepository.FindReviewedOrderIdAsync(parseResult.OrderCode.ToString(), cancellationToken);
        if (reviewedId is Guid orderId)
            return await _unitOfWork.WithLocksAsync([orderId], () => RecordTransferAsync(orderId, parseResult, cancellationToken), cancellationToken);

        var eventId = parseResult.BankReference ?? parseResult.GatewayTransactionId ?? parseResult.OrderCode.ToString();

        // 2. Check duplicate webhook idempotency
        var existingWebhook = await _webhookRepository.GetByProviderAndEventIdAsync("PAYOS", eventId, cancellationToken);
        if (existingWebhook != null && existingWebhook.ProcessingState == WebhookProcessingState.PROCESSED)
        {
            _logger.LogInformation("PayOS webhook event {EventId} was already processed. Replaying success response.", eventId);
            return new ProcessPayOsWebhookResult(true, "The webhook was already processed successfully (Idempotent replay).");
        }

        if (existingWebhook == null)
        {
            existingWebhook = new ProviderWebhook
            {
                Provider = "PAYOS",
                ProviderEventId = eventId,
                ProcessingState = WebhookProcessingState.PENDING,
                Payload = request.RawPayload,
                ReceivedAt = DateTimeOffset.UtcNow
            };
            await _webhookRepository.AddAsync(existingWebhook, cancellationToken);
        }

        // 3. Find payment order
        var order = await _orderRepository.GetByOrderCodeAsync(parseResult.OrderCode.ToString(), cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("PaymentOrder with OrderCode {OrderCode} not found for webhook event {EventId}.", parseResult.OrderCode, eventId);
            existingWebhook.MarkFailed();
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ProcessPayOsWebhookResult(false, $"No payment order was found for order code {parseResult.OrderCode}.", "ORDER_NOT_FOUND");
        }

        // 4. Validate amount
        if ((long)order.FinalAmount != parseResult.AmountVnd)
        {
            _logger.LogError("Payment amount mismatch for OrderCode {OrderCode}: expected {Expected}, received {Received}.",
                order.OrderCode, order.FinalAmount, parseResult.AmountVnd);

            existingWebhook.MarkFailed();
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ProcessPayOsWebhookResult(false, "The payment amount does not match the order amount.", "AMOUNT_MISMATCH");
        }

        // 5. Update state on SUCCESS
        if (parseResult.PaymentStatus == "SUCCESS")
        {
            var now = DateTimeOffset.UtcNow;
            var isAlreadySettled = order.Transactions.Any(t =>
                t.Status == PaymentStatus.SUCCESS &&
                !string.IsNullOrWhiteSpace(parseResult.GatewayTransactionId) &&
                t.GatewayTransactionId == parseResult.GatewayTransactionId);

            if (isAlreadySettled)
            {
                _logger.LogInformation("GatewayTransactionId {GatewayTransactionId} for OrderCode {OrderCode} is already settled. Replaying success response without duplicate processing.",
                    parseResult.GatewayTransactionId, order.OrderCode);
                existingWebhook.MarkProcessed();
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return new ProcessPayOsWebhookResult(true, "Transaction already settled successfully (Idempotent replay).");
            }

            var isLateOrExtra = order.Status == PaymentOrderStatus.PAID ||
                                order.Status == PaymentOrderStatus.CANCELLED ||
                                order.IsExpired(now);

            // Find pending transaction matching gatewayTx or first pending attempt for this order
            var tx = order.Transactions.FirstOrDefault(t =>
                t.Status == PaymentStatus.PENDING &&
                (string.IsNullOrWhiteSpace(t.GatewayTransactionId) ||
                 string.IsNullOrWhiteSpace(parseResult.GatewayTransactionId) ||
                 t.GatewayTransactionId == parseResult.GatewayTransactionId))
                ?? order.Transactions.FirstOrDefault(t => t.Status == PaymentStatus.PENDING);

            if (tx == null)
            {
                tx = new PaymentTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = order.TenantId,
                    PaymentOrderId = order.Id,
                    TransactionType = TransactionType.PAYMENT,
                    PaymentMethod = PaymentMethod.PAYOS,
                    GatewayTransactionId = parseResult.GatewayTransactionId,
                    Amount = parseResult.AmountVnd,
                    Status = PaymentStatus.PENDING,
                    CreatedAt = now
                };
                await _transactionRepository.AddAsync(tx, cancellationToken);
            }

            tx.MarkSuccess(parseResult.GatewayTransactionId, parseResult.TransactionTime);

            if (!isLateOrExtra)
            {
                order.MarkPaid(parseResult.TransactionTime);
            }
            else
            {
                _logger.LogWarning("Late or extra collection detected for OrderCode {OrderCode}, TxId {TxId}. Order status remains {Status}.",
                    order.OrderCode, tx.Id, order.Status);
            }

            existingWebhook.MarkProcessed();

            // Finalize invoice if exists and not late/extra
            var invoice = await _invoiceRepository.GetByPaymentOrderIdAsync(order.Id, cancellationToken);
            if (invoice != null && !isLateOrExtra)
            {
                invoice.PricingSnapshot.Status = "PAID";
                invoice.PricingSnapshot.IsSettled = true;
                invoice.PricingSnapshot.PaidAt = parseResult.TransactionTime;
                invoice.PricingSnapshot.PaymentMethod = "PAYOS";
                invoice.PricingSnapshot.TransactionReference = tx.Id.ToString();
                invoice.PricingSnapshot.GatewayTransactionId = parseResult.GatewayTransactionId;
                _invoiceRepository.Update(invoice);
            }

            // Stage Outbox Event PaymentSucceededV1 BEFORE SaveChangesAsync to guarantee atomic commit!
            var outboxEvent = new PaymentSucceededV1
            {
                EventId = Guid.NewGuid(),
                OccurredAt = parseResult.TransactionTime,
                CorrelationId = order.OrderCode,
                PaymentOrderId = order.Id,
                PaymentTransactionId = tx.Id,
                PaymentMethod = "PAYOS",
                GatewayReference = parseResult.GatewayTransactionId,
                TenantId = order.TenantId,
                SiteId = order.SiteId,
                ReferenceType = order.ReferenceType.ToString(),
                ReferenceId = order.ReferenceId,
                PlateNumber = invoice?.PricingSnapshot.PlateNumber,
                AccountId = order.AccountId,
                Amount = parseResult.AmountVnd,
                ParkingFee = invoice?.BaseParkingFee ?? order.FinalAmount,
                PenaltyFee = invoice?.OverstayPenaltyFee,
                PenaltyReason = invoice?.PricingSnapshot.LineItems.FirstOrDefault(l => l.ItemType == "PENALTY_OVERSTAY")?.Description,
                Currency = invoice?.PricingSnapshot.Currency ?? "VND"
            };

            await _eventPublisher.PublishAsync(outboxEvent, cancellationToken);

            // Atomic DB Commit: order + transaction + invoice + outboxMessage in one transaction
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully processed payOS payment webhook for OrderCode {OrderCode}, TxId {TxId}, staged PaymentSucceededV1 into outbox.", order.OrderCode, tx.Id);
        }

        return new ProcessPayOsWebhookResult(true, "Webhook processed successfully.");
    }

    private async Task<ProcessPayOsWebhookResult> RecordTransferAsync(Guid orderId, PaymentCallbackResult callback, CancellationToken ct)
    {
        if (callback.VerifiedTransferTime is not { } transferTime || transferTime == default || callback.AmountVnd <= 0 ||
            string.IsNullOrWhiteSpace(callback.BankReference) || callback.BankReference.Length > 255 ||
            string.IsNullOrWhiteSpace(callback.GatewayTransactionId) || callback.GatewayTransactionId.Length > 255)
            return new(false, "Missing verified transfer evidence.", "INVALID_PROVIDER_EVIDENCE");

        var order = await _orderRepository.GetByIdAsync(orderId, ct) ?? throw new InvalidOperationException("Missing order.");
        if (order.ReviewedMethod != nameof(PaymentMethod.PAYOS) || order.ReviewedCurrency != "VND")
            return new(false, "The reviewed method or currency does not match this PayOS transfer.", "COLLECTION_BINDING_CONFLICT");

        var checkouts = order.Transactions.Where(t => t.TransactionType == TransactionType.PAYMENT &&
            t.PaymentMethod == PaymentMethod.PAYOS && t.TenantId == order.TenantId).ToList();
        var linked = checkouts.Where(t => !string.IsNullOrWhiteSpace(t.GatewayTransactionId)).ToList();
        // Additional transfers share the order's checkout. Only that checkout row carries the link ID.
        if (linked.Count != 1 || linked[0].GatewayTransactionId != callback.GatewayTransactionId)
        {
            if (linked.Count == 0 && checkouts.Any(t => t.Status is PaymentStatus.PENDING or PaymentStatus.UNKNOWN))
                return new(false, "The PayOS checkout link has not been recorded yet.", "PAYOS_CHECKOUT_LINK_UNAVAILABLE", Retryable: true);

            return new(false, "The transfer does not match the recorded PayOS checkout.", "COLLECTION_BINDING_CONFLICT");
        }

        var prior = await _transactionRepository.GetByProviderCollectionReferenceAsync(PaymentMethod.PAYOS, callback.BankReference, ct);
        if (prior != null)
            return prior.HasVerifiedProviderCollection() && prior.PaymentOrderId == order.Id && prior.TenantId == order.TenantId &&
                prior.Amount == callback.AmountVnd && prior.PaidAt == transferTime
                ? new(true, "Transfer already recorded.")
                : new(false, "Conflicting transfer evidence.", "COLLECTION_BINDING_CONFLICT");

        var verifiedAt = await _unitOfWork.DatabaseNowAsync(ct);
        var settlesOrder = order.CanSettleReviewedPayment(callback.AmountVnd);
        var checkout = linked[0];
        var reuseCheckout = settlesOrder && checkout.ProviderCollectionReference is null &&
            checkout.Status is PaymentStatus.PENDING or PaymentStatus.UNKNOWN;
        var tx = reuseCheckout ? checkout : new PaymentTransaction
        {
            Id = Guid.NewGuid(), PaymentOrderId = order.Id, TenantId = order.TenantId,
            PaymentMethod = PaymentMethod.PAYOS, CreatedAt = verifiedAt
        };
        tx.RecordVerifiedProviderCollection(callback.BankReference, callback.AmountVnd, transferTime, verifiedAt);
        if (!reuseCheckout)
            await _transactionRepository.AddAsync(tx, ct);

        if (settlesOrder)
        {
            order.MarkPaid(verifiedAt);
            await _eventPublisher.PublishAsync(new PaymentSucceededV1
            {
                EventId = tx.Id, OccurredAt = transferTime, CorrelationId = order.OrderCode,
                PaymentOrderId = order.Id, PaymentTransactionId = tx.Id, PaymentMethod = order.ReviewedMethod!,
                GatewayReference = callback.GatewayTransactionId, TenantId = order.TenantId, SiteId = order.SiteId,
                ReferenceType = order.ReferenceType.ToString(), ReferenceId = order.ReferenceId, PlateNumber = order.PlateNumber,
                AccountId = order.AccountId, Amount = tx.Amount, ParkingFee = order.FinalAmount, Currency = order.ReviewedCurrency!
            }, ct);
        }
        else
        {
            var dispositionReason = order.Status switch
            {
                PaymentOrderStatus.PAID => "ADDITIONAL_COLLECTION",
                PaymentOrderStatus.CANCELLED => "ORDER_CANCELLED",
                _ => null
            };
            if (dispositionReason is not null)
                await _refundRepository.AddAsync(RefundRequest.ForVerifiedCollection(tx, dispositionReason), ct);

            _logger.LogWarning("Recorded verified transfer {TransactionId} without settling reviewed order {OrderId} in status {OrderStatus}.",
                tx.Id, order.Id, order.Status);
        }
        await _unitOfWork.SaveChangesAsync(ct);
        return new(true, "Verified transfer recorded.");
    }
}
