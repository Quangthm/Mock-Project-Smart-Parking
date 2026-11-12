using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.ReconcileReservation;

public sealed class ReconcileReservationCollectionCommandValidator : AbstractValidator<ReconcileReservationCollectionCommand>
{
    public ReconcileReservationCollectionCommandValidator()
    {
        RuleFor(x => x.Request.ReservationId)
            .NotEmpty()
            .WithMessage("ReservationId is required.");

        RuleFor(x => x.Request.CollectionId)
            .NotEmpty()
            .WithMessage("CollectionId is required.");

        RuleFor(x => x.Request.Reason)
            .NotEmpty()
            .WithMessage("Reason is required.")
            .MaximumLength(100);
    }
}
