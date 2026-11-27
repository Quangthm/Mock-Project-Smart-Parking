using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Events;

namespace SmartParking.PaymentService.Application.Reservations;

public sealed class ReviewedPayOsSettlement(
    IPaymentOrderRepository orderRepository,
    IPaymentTransactionRepository transactionRepository,
    IRefundRequestRepository refundRepository,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork,
    ILogger<ReviewedPayOsSettlement> logger)
{
    public async Task<ProcessPayOsWebhookResult> RecordAsync(Guid orderId, ReviewedPayOsTransfer transfer, CancellationToken ct)
    {
        var results = await RecordTransfersAsync(orderId, [transfer], ct);
        return results[0];
    }

    public async Task<IReadOnlyList<ProcessPayOsWebhookResult>> RecordTransfersAsync(Guid orderId, IReadOnlyList<ReviewedPayOsTransfer> transfers, CancellationToken ct)
    {
        var results = await unitOfWork.WithLocksAsync([orderId], async () =>
        {
            var recorded = new List<ProcessPayOsWebhookResult>(transfers.Count);
            foreach (var transfer in transfers)
                recorded.Add(await RecordTransferAsync(orderId, transfer, ct));
            return recorded;
        }, ct);

        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            if (result.RecordedTransactionId is { } transactionId)
                logger.LogInformation("Reviewed PayOS transaction committed. OrderCode {OrderCode}, PaymentOrderId {PaymentOrderId}, PaymentTransactionId {PaymentTransactionId}, settled order {SettledOrder}",
                    transfers[index].OrderCode, orderId, transactionId, result.SettledOrder);
        }
        return results;
    }

    private async Task<ProcessPayOsWebhookResult> RecordTransferAsync(Guid orderId, ReviewedPayOsTransfer transfer, CancellationToken ct)
    {
        ProcessPayOsWebhookResult Reject(string code, string reason, string message, bool retryable = false)
        {
            logger.LogWarning("Reviewed PayOS transfer rejected. OrderCode {OrderCode}, PaymentOrderId {PaymentOrderId}, code {ErrorCode}, reason {Reason}",
                transfer.OrderCode, orderId, code, reason);
            return new(false, message, code, retryable);
        }

        const string invalidEvidence = "INVALID_PROVIDER_EVIDENCE";
        const string evidenceMessage = "Missing verified transfer evidence.";
        if (transfer.OccurredAt is not { } transferTime || transferTime == default)
            return Reject(invalidEvidence, transfer.EvidenceFailure ?? "TRANSFER_TIMESTAMP", evidenceMessage);
        if (transfer.AmountVnd <= 0)
            return Reject(invalidEvidence, "TRANSFER_AMOUNT", evidenceMessage);
        if (string.IsNullOrWhiteSpace(transfer.BankReference) || transfer.BankReference.Length > 255)
            return Reject(invalidEvidence, "TRANSFER_REFERENCE", evidenceMessage);
        if (string.IsNullOrWhiteSpace(transfer.PaymentLinkId) || transfer.PaymentLinkId.Length > 255)
            return Reject(invalidEvidence, "PAYMENT_LINK_ID", evidenceMessage);

        var order = await orderRepository.GetByIdAsync(orderId, ct) ?? throw new InvalidOperationException("Missing reviewed order.");
        if (!long.TryParse(order.OrderCode, out var orderCode) || orderCode != transfer.OrderCode ||
            order.ReviewedMethod != nameof(PaymentMethod.PAYOS) || order.ReviewedCurrency != "VND")
            return Reject("COLLECTION_BINDING_CONFLICT", "REVIEWED_ORDER_METHOD_OR_CURRENCY", "The reviewed method or currency does not match this PayOS transfer.");

        var checkouts = order.Transactions.Where(t => t.TransactionType == TransactionType.PAYMENT &&
            t.PaymentMethod == PaymentMethod.PAYOS && t.TenantId == order.TenantId).ToList();
        var linked = checkouts.Where(t => !string.IsNullOrWhiteSpace(t.GatewayTransactionId)).ToList();
        // Additional transfers share the order's checkout. Only that checkout row carries the link ID.
        if (linked.Count != 1 || linked[0].GatewayTransactionId != transfer.PaymentLinkId)
        {
            if (linked.Count == 0 && checkouts.Any(t => t.Status is PaymentStatus.PENDING or PaymentStatus.UNKNOWN))
                return Reject("PAYOS_CHECKOUT_LINK_UNAVAILABLE", "CHECKOUT_LINK_PENDING", "The PayOS checkout link has not been recorded yet.", retryable: true);

            return Reject("COLLECTION_BINDING_CONFLICT", "CHECKOUT_LINK_MISMATCH", "The transfer does not match the recorded PayOS checkout.");
        }

        var prior = await transactionRepository.GetByProviderCollectionReferenceAsync(PaymentMethod.PAYOS, transfer.BankReference, ct);
        if (prior != null)
        {
            if (!prior.HasVerifiedProviderCollection() || prior.PaymentOrderId != order.Id || prior.TenantId != order.TenantId ||
                prior.Amount != transfer.AmountVnd || prior.PaidAt != transferTime)
                return Reject("COLLECTION_BINDING_CONFLICT", "TRANSFER_REFERENCE_CONFLICT", "Conflicting transfer evidence.");
            logger.LogInformation("Reviewed PayOS transfer replay. PaymentOrderId {PaymentOrderId}, PaymentTransactionId {PaymentTransactionId}",
                order.Id, prior.Id);
            return new(true, "Transfer already recorded.");
        }

        var verifiedAt = await unitOfWork.DatabaseNowAsync(ct);
        var settlesOrder = order.CanSettleReviewedPayment(transfer.AmountVnd);
        var checkout = linked[0];
        logger.LogInformation("Reviewed PayOS transfer validated. PaymentOrderId {PaymentOrderId}, CheckoutTransactionId {CheckoutTransactionId}, expected VND {ExpectedAmount}, received VND {TransferredAmount}, settles order {SettlesOrder}",
            order.Id, checkout.Id, order.FinalAmount, transfer.AmountVnd, settlesOrder);
        var reuseCheckout = settlesOrder && checkout.ProviderCollectionReference is null &&
            checkout.Status is PaymentStatus.PENDING or PaymentStatus.UNKNOWN;
        var tx = reuseCheckout ? checkout : new PaymentTransaction
        {
            Id = Guid.NewGuid(), PaymentOrderId = order.Id, TenantId = order.TenantId,
            PaymentMethod = PaymentMethod.PAYOS, CreatedAt = verifiedAt
        };
        tx.RecordVerifiedProviderCollection(transfer.BankReference, transfer.AmountVnd, transferTime, verifiedAt);
        if (!reuseCheckout)
            await transactionRepository.AddAsync(tx, ct);

        if (settlesOrder)
        {
            order.MarkPaid(verifiedAt);
            await eventPublisher.PublishAsync(new PaymentSucceededV1
            {
                EventId = tx.Id, OccurredAt = transferTime, CorrelationId = order.OrderCode,
                SettlementClassification = PaymentSucceededV1.ReviewedReservationOrderPaid,
                PaymentOrderId = order.Id, PaymentTransactionId = tx.Id, PaymentMethod = order.ReviewedMethod!,
                GatewayReference = transfer.PaymentLinkId, TenantId = order.TenantId, SiteId = order.SiteId,
                ReferenceType = order.ReferenceType.ToString(), ReferenceId = order.ReferenceId, PlateNumber = order.PlateNumber,
                AccountId = order.AccountId, Amount = tx.Amount, ParkingFee = order.FinalAmount, Currency = order.ReviewedCurrency!
            }, ct);
            logger.LogInformation("Reviewed PaymentSucceededV1 staged. EventId {EventId}, PaymentOrderId {PaymentOrderId}, ReservationId {ReservationId}",
                tx.Id, order.Id, order.ReferenceId);
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
                await refundRepository.AddAsync(RefundRequest.ForVerifiedCollection(tx, dispositionReason), ct);

            logger.LogWarning("Reviewed PayOS non-settling collection staged. PaymentOrderId {PaymentOrderId}, PaymentTransactionId {PaymentTransactionId}, order status {OrderStatus}, disposition {Disposition}",
                order.Id, tx.Id, order.Status, dispositionReason);
        }
        await unitOfWork.SaveChangesAsync(ct);
        return new(true, "Verified transfer recorded.") { RecordedTransactionId = tx.Id, SettledOrder = settlesOrder };
    }
}
