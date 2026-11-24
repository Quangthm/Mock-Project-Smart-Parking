using FluentValidation;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ReadOwnBookingsValidator : AbstractValidator<ReadOwnBookingsQuery>
{
    public ReadOwnBookingsValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x).Must(x => (x.Before is null) == (x.BeforeId is null));
        RuleFor(x => x.BeforeId).Must(x => x is null || x != Guid.Empty);
        RuleFor(x => x).Must(x => (x.From is null) == (x.To is null));
        RuleFor(x => x).Must(x => x.From is null || x.To > x.From);
    }
}
