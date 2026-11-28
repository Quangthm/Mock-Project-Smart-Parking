using FluentValidation;

namespace SmartParking.FacilityService.Application.Reservations;

public sealed class CheckReservationSlotEligibilityValidator : AbstractValidator<CheckReservationSlotEligibilityQuery>
{
    public CheckReservationSlotEligibilityValidator()
    {
        RuleFor(query => query.SlotId).NotEmpty();
        RuleFor(query => query.Start).NotEmpty().Must(time => time.Offset == TimeSpan.Zero);
        RuleFor(query => query.End).NotEmpty().GreaterThan(query => query.Start).Must(time => time.Offset == TimeSpan.Zero);
    }
}
