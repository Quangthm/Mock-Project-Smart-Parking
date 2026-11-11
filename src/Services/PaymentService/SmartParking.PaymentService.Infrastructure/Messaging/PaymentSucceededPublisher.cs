using MassTransit;
using SmartParking.PaymentService.Application.Common.Interfaces.Services;
using SmartParking.PaymentService.Domain.Events;

namespace SmartParking.PaymentService.Infrastructure.Messaging;

public sealed class PaymentSucceededPublisher(IPublishEndpoint publisher)
    : IPaymentSucceededPublisher
{
    public Task PublishAsync(PaymentSucceededMessage message, CancellationToken ct) =>
        publisher.Publish(new PaymentSucceededV1
        {
            EventId = Guid.NewGuid(),
            OccurredAt = message.OccurredAt,
            CorrelationId = message.PaymentOrderId.ToString(),
            PaymentOrderId = message.PaymentOrderId,
            PaymentTransactionId = message.PaymentTransactionId,
            PaymentMethod = message.PaymentMethod,
            GatewayReference = message.GatewayReference,
            TenantId = message.TenantId,
            SiteId = message.SiteId,
            ReferenceType = message.ReferenceType,
            ReferenceId = message.ReferenceId,
            PlateNumber = message.PlateNumber,
            AccountId = message.AccountId,
            Amount = message.Amount,
            ParkingFee = message.ParkingFee,
            PenaltyFee = message.PenaltyFee,
            PenaltyReason = message.PenaltyReason,
            Currency = message.Currency
        }, ct);
}
