using FluentValidation;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CheckInWalkInCommandValidator : AbstractValidator<CheckInWalkInCommand>
{
    public CheckInWalkInCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.PlateNumber)
            .Must(plate => Passes(() => GateRequestValidation.ValidatePlate(plate, "Enter a valid vehicle plate.")))
            .WithMessage("Enter a valid vehicle plate.");
        RuleFor(x => x.ImageData)
            .Must(imageData => Passes(() => GateRequestValidation.ValidateImageData(imageData)))
            .WithMessage("Gate image must be a JPEG, PNG, or WebP data URL no larger than 14 MB.");
        RuleFor(x => x.VehicleType)
            .Must(vehicleType => GateRequestValidation.NormalizeVehicleType(vehicleType) is not null)
            .WithMessage("Vehicle type must be CAR or MOTORCYCLE.");
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