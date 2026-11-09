using FluentValidation;
using SmartParking.OperationService.Domain;
namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

internal static class GateInput
{
    public static bool ValidPlate(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 50 && GateRules.NormalizePlate(value).Length >= 2;
    public static bool ValidImage(string? value) => value is null || value.Length <= 14 * 1024 * 1024 &&
        (value.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase) ||
         value.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) ||
         value.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase));
}
public sealed class FindWalkInValidator : AbstractValidator<FindWalkInSessionQuery>
{
    public FindWalkInValidator() => RuleFor(x => x.Plate).Must(GateInput.ValidPlate).WithMessage("Enter a valid vehicle plate.");
}
public sealed class CheckInWalkInValidator : AbstractValidator<CheckInWalkInCommand>
{
    public CheckInWalkInValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.PlateNumber).Must(GateInput.ValidPlate);
        RuleFor(x => x.VehicleType).Must(x => GateRules.NormalizeVehicleType(x) is not null);
        RuleFor(x => x.ImageData).Must(GateInput.ValidImage);
    }
}
public sealed class CheckOutWalkInValidator : AbstractValidator<CheckOutWalkInCommand>
{
    public CheckOutWalkInValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty(); RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.PlateNumber).Must(GateInput.ValidPlate);
        RuleFor(x => x.ImageData).Must(GateInput.ValidImage);
    }
}
public sealed class CheckInReservationValidator : AbstractValidator<CheckInReservationCommand>
{
    public CheckInReservationValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty(); RuleFor(x => x.ReservationId).NotEmpty();
        RuleFor(x => x.PlateNumber).MaximumLength(50);
        RuleFor(x => x.ImageData).Must(GateInput.ValidImage);
    }
}
public sealed class CheckOutReservationValidator : AbstractValidator<CheckOutReservationCommand>
{
    public CheckOutReservationValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty(); RuleFor(x => x.ReservationId).NotEmpty();
        RuleFor(x => x.PlateNumber).MaximumLength(50);
        RuleFor(x => x.ImageData).Must(GateInput.ValidImage);
    }
}
