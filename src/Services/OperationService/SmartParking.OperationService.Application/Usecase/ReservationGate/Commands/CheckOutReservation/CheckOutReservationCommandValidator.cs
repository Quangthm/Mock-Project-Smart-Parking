using FluentValidation;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CheckOutReservationCommandValidator : AbstractValidator<CheckOutReservationCommand>
{
    public CheckOutReservationCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.ReservationId).NotEmpty().WithMessage("ReservationId is required.");
        RuleFor(x => x.PlateNumber)
            .Must(plate => Passes(() => GateRequestValidation.ValidateReservationAction(plate, null)))
            .WithMessage("Plate number is too long.");
        RuleFor(x => x.ImageData)
            .Must(imageData => Passes(() => GateRequestValidation.ValidateReservationAction(null, imageData)))
            .WithMessage("Gate image must be a JPEG, PNG, or WebP data URL no larger than 14 MB.");
    }

    private static bool Passes(Action validation)
    {
        try
        {
            validation();
            return true;
        }
        catch (ReservationGateException)
        {
            return false;
        }
    }
}