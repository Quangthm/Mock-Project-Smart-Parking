using FluentValidation;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessReservationNotification;

public class ProcessReservationNotificationCommandValidator : AbstractValidator<ProcessReservationNotificationCommand>
{
    public ProcessReservationNotificationCommandValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("EventId is required.");

        RuleFor(x => x.ReservationId)
            .NotEmpty().WithMessage("ReservationId is required.");

        RuleFor(x => x.DriverId)
            .NotEmpty().WithMessage("DriverId is required.");

        RuleFor(x => x.SlotCode)
            .NotEmpty().WithMessage("SlotCode is required.");
    }
}
