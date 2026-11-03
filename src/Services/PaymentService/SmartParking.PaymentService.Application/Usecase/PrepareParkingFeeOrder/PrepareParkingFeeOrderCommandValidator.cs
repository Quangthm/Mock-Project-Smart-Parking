using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.PrepareParkingFeeOrder;

public sealed class PrepareParkingFeeOrderCommandValidator : AbstractValidator<PrepareParkingFeeOrderCommand>
{
    public PrepareParkingFeeOrderCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required.");

        RuleFor(x => x.SiteId)
            .NotEmpty().WithMessage("SiteId is required.");

        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("SessionId is required.");

        RuleFor(x => x.VehicleType)
            .NotEmpty().WithMessage("VehicleType is required.")
            .Must(vt => vt is "CAR" or "MOTORCYCLE" or "OVERSIZED")
            .WithMessage("VehicleType must be CAR, MOTORCYCLE, or OVERSIZED.");

        RuleFor(x => x.PlateNumber)
            .NotEmpty().WithMessage("PlateNumber is required.");

        RuleFor(x => x.EntryTime)
            .NotEmpty().WithMessage("EntryTime is required.");

        RuleFor(x => x.PricingCutoffAt)
            .NotEmpty().WithMessage("PricingCutoffAt is required.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("IdempotencyKey is required.")
            .MaximumLength(255).WithMessage("IdempotencyKey cannot exceed 255 characters.");

        RuleFor(x => x.PenaltyAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PenaltyAmount.HasValue)
            .WithMessage("PenaltyAmount must be greater than or equal to 0.");

        RuleFor(x => x.PenaltyReason)
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.PenaltyReason))
            .WithMessage("PenaltyReason cannot exceed 255 characters.");
    }
}
