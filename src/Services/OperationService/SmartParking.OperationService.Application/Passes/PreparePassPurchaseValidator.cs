using FluentValidation;

namespace SmartParking.OperationService.Application.Passes;

public sealed class PreparePassPurchaseValidator : AbstractValidator<PreparePassPurchaseCommand>
{
    public PreparePassPurchaseValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.PlanRevision).GreaterThan(0);
        RuleFor(x => x.FinancialTermId).NotEmpty();
        RuleFor(x => x.FinancialTermRevision).GreaterThan(0);
        RuleFor(x => x.ProductType).Equal("MONTHLY");
        RuleFor(x => x.VehicleType).Must(x => x is "CAR" or "MOTORCYCLE" or "OVERSIZED");
    }
}
