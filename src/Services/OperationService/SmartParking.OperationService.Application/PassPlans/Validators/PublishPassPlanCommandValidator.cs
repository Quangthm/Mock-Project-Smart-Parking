using FluentValidation;
using SmartParking.OperationService.Application.PassPlans.Commands;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Validators;

public sealed class PublishPassPlanCommandValidator : AbstractValidator<PublishPassPlanCommand>
{
    public PublishPassPlanCommandValidator()
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

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(128)
            .When(x => !string.IsNullOrEmpty(x.IdempotencyKey))
            .WithErrorCode("VALIDATION_ERROR");

        RuleFor(x => x.Request!.PublishNote)
            .MaximumLength(1000)
            .When(x => x.Request is not null && !string.IsNullOrEmpty(x.Request.PublishNote))
            .WithErrorCode("VALIDATION_ERROR");
    }
}
