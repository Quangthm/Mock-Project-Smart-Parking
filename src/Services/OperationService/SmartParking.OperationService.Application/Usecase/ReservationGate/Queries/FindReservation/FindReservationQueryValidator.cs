using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class FindReservationQueryValidator : AbstractValidator<FindReservationQuery>
{
    public FindReservationQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.Query)
            .Must(query => !string.IsNullOrWhiteSpace(query) && query.Length <= 100)
            .WithMessage("Enter a reservation ID or plate.");
    }
}