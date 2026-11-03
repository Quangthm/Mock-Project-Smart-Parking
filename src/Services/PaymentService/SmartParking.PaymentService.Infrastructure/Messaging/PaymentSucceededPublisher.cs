using MassTransit;
using SmartParking.PaymentService.Application.Common.Interfaces.Services;

namespace SmartParking.PaymentService.Infrastructure.Messaging;

public sealed record PaymentSucceededV1(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    Guid PaymentOrderId,
    Guid PaymentTransactionId,
    string PaymentMethod,
    string? GatewayReference,
    Guid TenantId,
    Guid SiteId,
    string ReferenceType,
    Guid ReferenceId,
    string? PlateNumber,
    Guid? AccountId,
    decimal Amount,
    decimal ParkingFee,
    decimal? PenaltyFee,
    string? PenaltyReason,
    string Currency);

public sealed class PaymentSucceededPublisher(IPublishEndpoint publisher)
    : IPaymentSucceededPublisher
{
    public Task PublishAsync(PaymentSucceededMessage message, CancellationToken ct) =>
        publisher.Publish(new PaymentSucceededV1(
            Guid.NewGuid(),
            message.OccurredAt,
            message.PaymentOrderId.ToString(),
            message.PaymentOrderId,
            message.PaymentTransactionId,
            message.PaymentMethod,
            message.GatewayReference,
            message.TenantId,
            message.SiteId,
            message.ReferenceType,
            message.ReferenceId,
            message.PlateNumber,
            message.AccountId,
            message.Amount,
            message.ParkingFee,
            message.PenaltyFee,
            message.PenaltyReason,
            message.Currency), ct);
}
