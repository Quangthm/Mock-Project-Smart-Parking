using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed class GetInternalPassFinancialTermsQueryValidator : AbstractValidator<GetInternalPassFinancialTermsQuery>
{
    public GetInternalPassFinancialTermsQueryValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty();

        RuleFor(x => x.PlanId)
            .Must(p => !p.HasValue || p.Value != Guid.Empty)
            .WithMessage("planId cannot be empty when specified.");
    }
}
