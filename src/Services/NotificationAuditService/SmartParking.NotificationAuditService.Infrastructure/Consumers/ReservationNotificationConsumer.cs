using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Contracts.Notifications;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessReservationNotification;

namespace SmartParking.NotificationAuditService.Infrastructure.Consumers;

public class ReservationNotificationConsumer : IConsumer<ReservationConfirmedNotificationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<ReservationNotificationConsumer> _logger;

    public ReservationNotificationConsumer(
        IMediator mediator,
        ILogger<ReservationNotificationConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReservationConfirmedNotificationEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Consuming reservation notification event {EventId} for reservation {ReservationId}, recipient {DriverId}.",
            message.EventId,
            message.ReservationId,
            message.DriverId);

        var command = new ProcessReservationNotificationCommand
        {
            EventId = message.EventId,
            ReservationId = message.ReservationId,
            DriverId = message.DriverId,
            SiteId = message.SiteId,
            SiteName = message.SiteName,
            SlotCode = message.SlotCode,
            StartTime = message.StartTime,
            EndTime = message.EndTime,
            OccurredAt = message.OccurredAt
        };

        var result = await _mediator.Send(command, context.CancellationToken);

        if (result.IsDuplicate)
        {
            _logger.LogInformation(
                "Consumer acknowledged duplicate reservation notification event {EventId}.",
                message.EventId);
        }
        else if (result.Success)
        {
            _logger.LogInformation(
                "Consumer successfully processed reservation notification for event {EventId} (NotificationId: {NotificationId}).",
                message.EventId,
                result.NotificationId);
        }
        else
        {
            _logger.LogWarning(
                "Reservation notification for event {EventId} resulted in delivery failure: {ErrorMessage}. Persisted independently.",
                message.EventId,
                result.ErrorMessage);
        }
    }
}
