using FluentValidation;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CheckOutWalkInCommandValidator : AbstractValidator<CheckOutWalkInCommand>
{
    public CheckOutWalkInCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("SessionId is required.");
        RuleFor(x => x.PlateNumber)
            .Must(plate => Passes(() => GateRequestValidation.ValidatePlate(plate, "Enter a valid vehicle plate.")))
            .WithMessage("Enter a valid vehicle plate.");
        RuleFor(x => x.ImageData)
            .Must(imageData => Passes(() => GateRequestValidation.ValidateImageData(imageData)))
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