using FluentValidation;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Queries.PreviewReservationPrice;

public sealed class PreviewReservationPriceQueryValidator : AbstractValidator<PreviewReservationPriceQuery>
{
    public PreviewReservationPriceQueryValidator()
    {
        RuleFor(x => x.Request.SiteId)
            .NotEmpty()
            .WithMessage("SiteId is required.");

        RuleFor(x => x.Request.RequestedSlotId)
            .NotEmpty()
            .WithMessage("RequestedSlotId is required.");

        RuleFor(x => x.Request.End)
            .GreaterThan(x => x.Request.Start)
            .WithMessage("End time must be greater than start time.");

        RuleFor(x => x.Request.VehicleType)
            .Must(v => string.IsNullOrWhiteSpace(v) || Enum.TryParse<PricingVehicleType>(v, true, out _))
            .WithMessage("VehicleType must be one of: CAR, MOTORCYCLE, OVERSIZED.");
    }
}
