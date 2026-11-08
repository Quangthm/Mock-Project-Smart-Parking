using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class ListReservationsQueryValidator : AbstractValidator<ListReservationsQuery>
{
    public ListReservationsQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
    }
}