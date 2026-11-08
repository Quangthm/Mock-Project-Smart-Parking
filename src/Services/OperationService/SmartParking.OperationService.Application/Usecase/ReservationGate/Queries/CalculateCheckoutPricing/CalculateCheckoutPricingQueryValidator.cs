using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CalculateCheckoutPricingQueryValidator : AbstractValidator<CalculateCheckoutPricingQuery>
{
    public CalculateCheckoutPricingQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.ReferenceId).NotEmpty().WithMessage("ReferenceId is required.");
        RuleFor(x => x.ReferenceType)
            .Must(referenceType => referenceType is "RESERVATION" or "PARKING_SESSION")
            .WithMessage("Checkout reference type is invalid.");
        RuleFor(x => x.EntryTime)
            .Must((query, entryTime) => entryTime <= query.ExitTime)
            .WithMessage("Checkout time range is invalid.");
    }
}