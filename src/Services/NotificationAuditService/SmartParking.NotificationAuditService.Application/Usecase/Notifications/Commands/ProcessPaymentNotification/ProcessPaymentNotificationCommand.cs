using MediatR;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessPaymentNotification;

public record ProcessPaymentNotificationCommand : IRequest<NotificationDeliveryResult>
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public Guid PaymentOrderId { get; init; }
    public Guid PaymentTransactionId { get; init; }
    public string PaymentMethod { get; init; } = "PAYOS";
    public string? GatewayReference { get; init; }
    public Guid TenantId { get; init; }
    public Guid SiteId { get; init; }
    public string ReferenceType { get; init; } = "PARKING_SESSION";
    public Guid ReferenceId { get; init; }
    public string? PlateNumber { get; init; }
    public Guid? AccountId { get; init; }
    public decimal Amount { get; init; }
    public decimal ParkingFee { get; init; }
    public decimal? PenaltyFee { get; init; }
    public string? PenaltyReason { get; init; }
    public string Currency { get; init; } = "VND";

    // Backward-compatibility aliases
    public Guid DriverId
    {
        get => AccountId ?? Guid.Empty;
        init => AccountId = value;
    }

    public string TransactionId
    {
        get => PaymentTransactionId != Guid.Empty ? PaymentTransactionId.ToString() : string.Empty;
        init
        {
            if (Guid.TryParse(value, out var parsed))
                PaymentTransactionId = parsed;
        }
    }
}
