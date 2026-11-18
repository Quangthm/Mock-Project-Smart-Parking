using FluentValidation;

namespace SmartParking.FacilityService.Application.Reservations;

public sealed class ResolveReservationSlotValidator : AbstractValidator<ResolveReservationSlotQuery>
{
    public ResolveReservationSlotValidator() => RuleFor(query => query.SlotId).NotEmpty();
}
