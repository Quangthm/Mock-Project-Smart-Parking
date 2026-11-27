using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed class GetPassRevenueSummaryQueryValidator : AbstractValidator<GetPassRevenueSummaryQuery>
{
    public GetPassRevenueSummaryQueryValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty();

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From.Value <= x.To.Value)
            .WithMessage("from must be less than or equal to to.");

        When(x => !string.IsNullOrWhiteSpace(x.ProductType), () =>
        {
            RuleFor(x => x.ProductType!)
                .Must(p => string.Equals(p.Trim(), "MONTHLY", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(p.Trim(), "YEARLY", StringComparison.OrdinalIgnoreCase))
                .WithMessage("productType must be MONTHLY or YEARLY.")
                .WithErrorCode("INVALID_PRODUCT_TYPE");
        });
    }
}
