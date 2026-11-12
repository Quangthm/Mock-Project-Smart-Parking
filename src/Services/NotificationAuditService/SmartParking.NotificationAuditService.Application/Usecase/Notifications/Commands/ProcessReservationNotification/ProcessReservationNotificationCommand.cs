using MediatR;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessReservationNotification;

public record ProcessReservationNotificationCommand : IRequest<NotificationDeliveryResult>
{
    public Guid EventId { get; init; }
    public Guid ReservationId { get; init; }
    public Guid DriverId { get; init; }
    public Guid SiteId { get; init; }
    public string SiteName { get; init; } = string.Empty;
    public string SlotCode { get; init; } = string.Empty;
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
