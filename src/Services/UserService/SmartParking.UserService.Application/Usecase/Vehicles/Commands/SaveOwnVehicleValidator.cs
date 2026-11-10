using FluentValidation;
using SmartParking.UserService.Domain.ValueObjects;

namespace UserService.Application.Usecase.Vehicles;

public sealed class SaveOwnVehicleValidator : AbstractValidator<SaveOwnVehicleCommand>
{
    public SaveOwnVehicleValidator()
    {
        RuleFor(x => x.Plate).NotEmpty().MaximumLength(30);
        RuleFor(x => x.VehicleType).Must(type => type is "CAR" or "MOTORCYCLE");
        RuleFor(x => x.ImageAssetId).NotNull().NotEqual(Guid.Empty);
        RuleFor(x => x).Must(x => LicensePlateNormalizer.TryNormalize(x.Plate, x.VehicleType, out _))
            .WithMessage("Plate does not match the supported application format.");
    }
}
