using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed class QuotePassFinancialTermsCommandValidator : AbstractValidator<QuotePassFinancialTermsCommand>
{
    private static readonly string[] AllowedVehicles = ["CAR", "MOTORCYCLE", "OVERSIZED"];

    public QuotePassFinancialTermsCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithErrorCode("INVALID_FINANCIAL_TERMS");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.SiteId)
                .NotEmpty()
                .WithErrorCode("INVALID_FINANCIAL_TERMS");

            RuleFor(x => x.Request.PlanId)
                .NotEmpty()
                .WithErrorCode("INVALID_FINANCIAL_TERMS");

            RuleFor(x => x.Request.PlanRevision)
                .GreaterThanOrEqualTo(1)
                .WithErrorCode("INVALID_FINANCIAL_TERMS");

            RuleFor(x => x.Request.ExpectedFinancialTermRevision)
                .GreaterThanOrEqualTo(1)
                .WithErrorCode("INVALID_FINANCIAL_TERMS");

            RuleFor(x => x.Request.ProductType)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(p => !string.IsNullOrWhiteSpace(p) &&
                           (string.Equals(p.Trim(), "MONTHLY", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.Trim(), "YEARLY", StringComparison.OrdinalIgnoreCase)))
                .WithMessage("productType must be MONTHLY or YEARLY.")
                .WithErrorCode("INVALID_PRODUCT_TYPE");

            RuleFor(x => x.Request.VehicleType)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(v => !string.IsNullOrWhiteSpace(v) &&
                           AllowedVehicles.Contains(v.Trim().ToUpperInvariant(), StringComparer.Ordinal))
                .WithMessage("vehicleType must be CAR, MOTORCYCLE, or OVERSIZED.")
                .WithErrorCode("INVALID_FINANCIAL_TERMS");
        });
    }
}
