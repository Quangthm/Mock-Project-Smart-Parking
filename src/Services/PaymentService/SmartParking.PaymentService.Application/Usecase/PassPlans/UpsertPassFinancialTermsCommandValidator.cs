using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed class UpsertPassFinancialTermsCommandValidator : AbstractValidator<UpsertPassFinancialTermsCommand>
{
    private static readonly string[] AllowedVehicles = ["CAR", "MOTORCYCLE", "OVERSIZED"];

    public UpsertPassFinancialTermsCommandValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty()
            .WithErrorCode("INVALID_FINANCIAL_TERMS");

        RuleFor(x => x.PlanId)
            .NotEmpty()
            .WithErrorCode("INVALID_FINANCIAL_TERMS");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(128)
            .When(x => !string.IsNullOrWhiteSpace(x.IdempotencyKey))
            .WithErrorCode("INVALID_FINANCIAL_TERMS");

        RuleFor(x => x.Request)
            .NotNull()
            .WithErrorCode("INVALID_FINANCIAL_TERMS");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.ProductType)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(p => !string.IsNullOrWhiteSpace(p) &&
                           (string.Equals(p.Trim(), "MONTHLY", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.Trim(), "YEARLY", StringComparison.OrdinalIgnoreCase)))
                .WithMessage("productType must be MONTHLY or YEARLY.")
                .WithErrorCode("INVALID_FINANCIAL_TERMS");

            RuleFor(x => x.Request.PlanRevision)
                .GreaterThanOrEqualTo(1)
                .WithMessage("planRevision must be at least 1.")
                .WithErrorCode("INVALID_FINANCIAL_TERMS");

            RuleFor(x => x.Request.Currency)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(c => !string.IsNullOrWhiteSpace(c) &&
                           string.Equals(c.Trim(), "VND", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Only VND currency is supported.")
                .WithErrorCode("INVALID_FINANCIAL_TERMS");

            RuleFor(x => x.Request.VehiclePrices)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .NotEmpty()
                .WithMessage("vehiclePrices must contain at least one vehicle price.")
                .WithErrorCode("INVALID_FINANCIAL_TERMS");

            When(x => x.Request.VehiclePrices is not null, () =>
            {
                RuleFor(x => x.Request.VehiclePrices)
                    .Must(prices =>
                    {
                        if (prices.Any(p => p is null || string.IsNullOrWhiteSpace(p.VehicleType)))
                        {
                            return false;
                        }

                        var normalized = prices
                            .Select(p => p.VehicleType.Trim().ToUpperInvariant())
                            .ToArray();
                        return normalized.Distinct(StringComparer.Ordinal).Count() == normalized.Length;
                    })
                    .WithMessage("vehiclePrices cannot contain null items or duplicate vehicle types.")
                    .WithErrorCode("INVALID_FINANCIAL_TERMS");

                RuleForEach(x => x.Request.VehiclePrices)
                    .Cascade(CascadeMode.Stop)
                    .NotNull()
                    .WithMessage("vehiclePrices items cannot be null.")
                    .WithErrorCode("INVALID_FINANCIAL_TERMS")
                    .ChildRules(vp =>
                    {
                        vp.RuleFor(p => p.VehicleType)
                            .Cascade(CascadeMode.Stop)
                            .NotEmpty()
                            .Must(v => !string.IsNullOrWhiteSpace(v) &&
                                       AllowedVehicles.Contains(v.Trim().ToUpperInvariant(), StringComparer.Ordinal))
                            .WithMessage("vehicleType must be CAR, MOTORCYCLE, or OVERSIZED.")
                            .WithErrorCode("INVALID_FINANCIAL_TERMS");

                        vp.RuleFor(p => p.FullPeriodPrice)
                            .Cascade(CascadeMode.Stop)
                            .GreaterThan(0m)
                            .WithMessage("fullPeriodPrice must be greater than 0.")
                            .WithErrorCode("INVALID_FINANCIAL_TERMS")
                            .Must(p => p == decimal.Truncate(p))
                            .WithMessage("fullPeriodPrice must be a whole VND amount.")
                            .WithErrorCode("INVALID_FINANCIAL_TERMS");
                    });
            });
        });
    }
}
