using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Lots.Queries.ListParkingLots;

public sealed class ListParkingLotsQueryValidator : AbstractValidator<ListParkingLotsQuery>
{
    public ListParkingLotsQueryValidator()
    {
    }
}
