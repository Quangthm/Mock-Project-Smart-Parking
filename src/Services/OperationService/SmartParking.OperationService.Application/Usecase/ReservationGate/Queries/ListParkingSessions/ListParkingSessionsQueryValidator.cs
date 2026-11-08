using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class ListParkingSessionsQueryValidator : AbstractValidator<ListParkingSessionsQuery>
{
    public ListParkingSessionsQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
    }
}