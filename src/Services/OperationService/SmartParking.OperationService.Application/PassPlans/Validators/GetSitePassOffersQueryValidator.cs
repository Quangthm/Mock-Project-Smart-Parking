using FluentValidation;
using SmartParking.OperationService.Application.PassPlans.Queries;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Validators;

public sealed class GetSitePassOffersQueryValidator : AbstractValidator<GetSitePassOffersQuery>
{
    public GetSitePassOffersQueryValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty()
            .WithErrorCode("VALIDATION_ERROR");

        When(x => !string.IsNullOrWhiteSpace(x.VehicleType), () =>
        {
            RuleFor(x => x.VehicleType!)
                .Must(v => Enum.TryParse<PassVehicleType>(v.Trim(), ignoreCase: true, out var parsed) &&
                           Enum.IsDefined(parsed))
                .WithMessage("vehicleType must be CAR, MOTORCYCLE, or OVERSIZED.")
                .WithErrorCode("VALIDATION_ERROR");
        });
    }
}
