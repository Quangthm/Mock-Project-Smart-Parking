using FluentValidation;
using SmartParking.OperationService.Application.PassPlans.Commands;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Validators;

public sealed class SetPassPlanEnablementCommandValidator : AbstractValidator<SetPassPlanEnablementCommand>
{
    public SetPassPlanEnablementCommandValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty()
            .WithErrorCode("VALIDATION_ERROR");

        RuleFor(x => x.ProductType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(pt => !string.IsNullOrWhiteSpace(pt) &&
                        Enum.TryParse<PassProductType>(pt.Trim(), ignoreCase: true, out var parsed) &&
                        Enum.IsDefined(parsed))
            .WithMessage("productType must be MONTHLY or YEARLY.")
            .WithErrorCode("VALIDATION_ERROR");

        RuleFor(x => x.Request)
            .NotNull()
            .WithErrorCode("VALIDATION_ERROR");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.IsEnabled)
                .NotNull()
                .WithMessage("isEnabled is required.")
                .WithErrorCode("VALIDATION_ERROR");

            RuleFor(x => x.Request.ExpectedPlanRevision)
                .GreaterThanOrEqualTo(1)
                .When(x => x.Request.ExpectedPlanRevision.HasValue)
                .WithErrorCode("VALIDATION_ERROR");
        });
    }
}
