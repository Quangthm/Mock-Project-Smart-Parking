using FluentValidation;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.CreateReservationQuote;

public sealed class CreateReservationQuoteCommandValidator : AbstractValidator<CreateReservationQuoteCommand>
{
    public CreateReservationQuoteCommandValidator()
    {
        RuleFor(x => x.Request.SiteId)
            .NotEmpty()
            .WithMessage("SiteId is required.");

        RuleFor(x => x.Request.RequestedSlotId)
            .NotEmpty()
            .WithMessage("RequestedSlotId is required.");

        RuleFor(x => x.Request.VehicleId)
            .NotEmpty()
            .WithMessage("VehicleId is required.");

        RuleFor(x => x.Request.PaymentMethod)
            .NotEmpty()
            .WithMessage("PaymentMethod is required.")
            .MaximumLength(32)
            .WithMessage("PaymentMethod cannot exceed 32 characters.");

        RuleFor(x => x.Request.End)
            .GreaterThan(x => x.Request.Start)
            .WithMessage("End time must be greater than start time.");

        RuleFor(x => x.Request.VehicleType)
            .Must(v => string.IsNullOrWhiteSpace(v) || Enum.TryParse<PricingVehicleType>(v, true, out _))
            .WithMessage("VehicleType must be one of: CAR, MOTORCYCLE, OVERSIZED.");

        RuleFor(x => x.Request.PrepaymentMode)
            .Must(p => string.IsNullOrWhiteSpace(p) || Enum.TryParse<PrepaymentMode>(p, true, out _))
            .WithMessage("PrepaymentMode must be one of: FULL, PERCENTAGE, FIXED.");
    }
}
