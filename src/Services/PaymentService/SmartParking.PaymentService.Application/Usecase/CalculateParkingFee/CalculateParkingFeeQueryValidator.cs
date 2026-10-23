using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.CalculateParkingFee;

public sealed class CalculateParkingFeeQueryValidator : AbstractValidator<CalculateParkingFeeQuery>
{
    public CalculateParkingFeeQueryValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithErrorCode("INVALID_INPUT")
            .WithMessage("TenantId is required.");

        RuleFor(x => x.SiteId)
            .NotEmpty()
            .WithErrorCode("INVALID_INPUT")
            .WithMessage("SiteId is required.");

        RuleFor(x => x.VehicleType)
            .NotEmpty()
            .WithErrorCode("INVALID_INPUT")
            .WithMessage("VehicleType is required.")
            .Must(v => v.Equals("CAR", StringComparison.OrdinalIgnoreCase) ||
                       v.Equals("MOTORCYCLE", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("UNSUPPORTED_VEHICLE_TYPE")
            .WithMessage("Unsupported vehicle type. Only CAR and MOTORCYCLE are currently supported.");

        RuleFor(x => x.ExitTime)
            .GreaterThanOrEqualTo(x => x.EntryTime)
            .WithErrorCode("INVALID_TIME_RANGE")
            .WithMessage("ExitTime must be greater than or equal to EntryTime.");

        RuleFor(x => x.PolicyOverride)
            .Null()
            .When(x => !x.IsTrustedInternalCaller)
            .WithErrorCode("FORBIDDEN")
            .WithMessage("Policy override is only allowed for authorized internal callers.");
    }
}
