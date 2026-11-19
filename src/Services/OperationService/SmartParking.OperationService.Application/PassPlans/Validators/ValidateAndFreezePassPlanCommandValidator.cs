using FluentValidation;
using SmartParking.OperationService.Application.PassPlans.Commands;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Validators;

public sealed class ValidateAndFreezePassPlanCommandValidator : AbstractValidator<ValidateAndFreezePassPlanCommand>
{
    public ValidateAndFreezePassPlanCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithErrorCode("VALIDATION_ERROR");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.SiteId)
                .NotEmpty()
                .WithErrorCode("VALIDATION_ERROR");

            RuleFor(x => x.Request.PlanId)
                .NotEmpty()
                .WithErrorCode("VALIDATION_ERROR");

            RuleFor(x => x.Request.ExpectedPlanRevision)
                .GreaterThanOrEqualTo(1)
                .WithErrorCode("VALIDATION_ERROR");

            RuleFor(x => x.Request.ProductType)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(pt => !string.IsNullOrWhiteSpace(pt) &&
                            Enum.TryParse<PassProductType>(pt.Trim(), ignoreCase: true, out var parsed) &&
                            Enum.IsDefined(parsed))
                .WithMessage("productType must be MONTHLY or YEARLY.")
                .WithErrorCode("VALIDATION_ERROR");

            RuleFor(x => x.Request.VehicleType)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(vt => !string.IsNullOrWhiteSpace(vt) &&
                            Enum.TryParse<PassVehicleType>(vt.Trim(), ignoreCase: true, out var parsed) &&
                            Enum.IsDefined(parsed))
                .WithMessage("vehicleType must be CAR, MOTORCYCLE, or OVERSIZED.")
                .WithErrorCode("VALIDATION_ERROR");
        });
    }
}
