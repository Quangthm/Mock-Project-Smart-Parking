using FluentValidation;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class PreviewReservationValidator : AbstractValidator<PreviewReservationPriceQuery>
{
    public PreviewReservationValidator()
    {
        RuleFor(x => x.Intent).NotNull();
        When(x => x.Intent is not null, () =>
        {
            RuleFor(x => x.Intent.SiteId).NotEmpty(); RuleFor(x => x.Intent.RequestedSlotId).NotEmpty();
            RuleFor(x => x.Intent.End).GreaterThan(x => x.Intent.Start);
            RuleFor(x => x.Intent.VehicleId).Must(x => x is null || x != Guid.Empty);
            RuleFor(x => x.Intent.PaymentMethod).MaximumLength(32);
        });
    }
}
