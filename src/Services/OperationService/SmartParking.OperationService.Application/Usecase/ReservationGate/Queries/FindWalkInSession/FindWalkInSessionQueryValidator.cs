using FluentValidation;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class FindWalkInSessionQueryValidator : AbstractValidator<FindWalkInSessionQuery>
{
    public FindWalkInSessionQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.Plate)
            .Must(plate => Passes(() => GateRequestValidation.ValidatePlate(plate, "Enter a valid vehicle plate.")))
            .WithMessage("Enter a valid vehicle plate.");
    }

    private static bool Passes(Action validation)
    {
        try
        {
            validation();
            return true;
        }
        catch (ReservationGateException)
        {
            return false;
        }
    }
}