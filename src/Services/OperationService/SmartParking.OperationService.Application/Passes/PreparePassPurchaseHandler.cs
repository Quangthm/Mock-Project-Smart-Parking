using MediatR;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Passes;

public sealed class PreparePassPurchaseHandler(
    IPassPlanSnapshotService plans,
    IPassFinancialTermsOwner financialTerms) : IRequestHandler<PreparePassPurchaseCommand, PreparePassPurchaseResult>
{
    public async Task<PreparePassPurchaseResult> Handle(PreparePassPurchaseCommand command, CancellationToken ct)
    {
        var plan = await plans.ValidateAndFreezeAsync(new(
            command.SiteId,
            command.PlanId,
            command.PlanRevision,
            command.ProductType,
            command.VehicleType), ct);

        if (plan.PlanId != command.PlanId || plan.SiteId != command.SiteId ||
            plan.PlanRevision != command.PlanRevision || plan.ProductType != command.ProductType ||
            plan.VehicleType != command.VehicleType || plan.EntitlementType != "WHEN_SPACE_AVAILABLE")
            throw InvalidOwner("The plan snapshot does not match the requested pass.");

        var quote = await financialTerms.QuoteSnapshotAsync(new(
            command.SiteId,
            command.PlanId,
            plan.PlanRevision,
            command.FinancialTermRevision,
            command.ProductType,
            command.VehicleType), ct);

        if (quote.FinancialTermId != command.FinancialTermId || quote.PlanId != command.PlanId ||
            quote.PlanRevision != plan.PlanRevision || quote.FinancialTermRevision != command.FinancialTermRevision ||
            quote.ProductType != command.ProductType || quote.VehicleType != command.VehicleType ||
            quote.FullPeriodPrice <= 0 || quote.Currency != "VND")
            throw InvalidOwner("The financial quote does not match the requested pass.");

        return new(plan, quote);
    }

    private static ReservationGateException InvalidOwner(string message) =>
        new(503, "PASS_SNAPSHOT_RESPONSE_INVALID", message);
}
