using MediatR;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessPaymentNotification;

public record ProcessPaymentNotificationCommand : IRequest<NotificationDeliveryResult>
{
    public Guid EventId { get; init; }
    public Guid PaymentOrderId { get; init; }
    public Guid DriverId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public string PaymentMethod { get; init; } = "PAYOS";
    public string TransactionId { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
}
