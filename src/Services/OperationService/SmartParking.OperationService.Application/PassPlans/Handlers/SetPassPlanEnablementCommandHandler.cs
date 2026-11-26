using MediatR;
using SmartParking.OperationService.Application.PassPlans.Commands;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Application.PassPlans.Services;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Handlers;

public sealed class SetPassPlanEnablementCommandHandler(
    IPassPlanFacilityScopePort scopePort,
    IPassPlanRepository repository,
    IPaymentPassTermsPort paymentTermsPort,
    TimeProvider clock)
    : IRequestHandler<SetPassPlanEnablementCommand, SetPassPlanEnablementResponseDto>
{
    public async Task<SetPassPlanEnablementResponseDto> Handle(SetPassPlanEnablementCommand command, CancellationToken ct)
    {
        var productType = PassPlanHelper.ParseProductType(command.ProductType);
        var scope = await scopePort.ValidateOwnerSiteAsync(command.SiteId, ct);
        var actorId = command.ActorUserId ?? scope.UserId;
        var isEnabled = command.Request.IsEnabled!.Value;

        if (isEnabled && productType == PassProductType.YEARLY)
        {
            throw new PassPlanException(
                422,
                "YEARLY_POLICY_BLOCKED_Q05",
                "Yearly parking products are currently blocked pending policy approval Q05.");
        }

        var preCheckPlan = await repository.GetPlanAsync(command.SiteId, productType, ct);
        if (preCheckPlan is null || preCheckPlan.ActiveRevision <= 0)
        {
            throw new PassPlanException(
                422,
                "PLAN_NOT_PUBLISHED",
                "Publish a pass plan revision before changing enablement.");
        }

        if (command.Request.ExpectedPlanRevision.HasValue &&
            command.Request.ExpectedPlanRevision.Value != preCheckPlan.ActiveRevision)
        {
            throw new PassPlanException(
                409,
                "PLAN_VERSION_CONFLICT",
                $"Expected plan revision {command.Request.ExpectedPlanRevision.Value}, but current active revision is {preCheckPlan.ActiveRevision}.");
        }

        if (isEnabled)
        {
            var activeRevision = await repository.GetRevisionAsync(preCheckPlan.Id, preCheckPlan.ActiveRevision, ct);
            var requiredVehicles = activeRevision?.VehicleTypes ?? [];
            var terms = await paymentTermsPort.GetActiveFinancialTermsAsync(command.SiteId, preCheckPlan.Id, ct);
            var readiness = PassPlanHelper.EvaluateFinancialReadiness(productType, preCheckPlan, requiredVehicles, terms);
            if (!readiness.HasCompleteFinancialTerms)
            {
                throw new PassPlanException(
                    422,
                    "MISSING_FINANCIAL_TERMS",
                    "Complete active financial terms for the active plan revision must be configured in PaymentService before enabling sales.");
            }
        }

        await using var tx = await repository.BeginSiteLockTransactionAsync(command.SiteId, ct);

        var plan = await repository.GetPlanAsync(command.SiteId, productType, ct);
        if (plan is null || plan.ActiveRevision <= 0)
        {
            throw new PassPlanException(
                422,
                "PLAN_NOT_PUBLISHED",
                "Publish a pass plan revision before changing enablement.");
        }

        if (isEnabled && plan.ActiveRevision != preCheckPlan.ActiveRevision)
        {
            throw new PassPlanException(
                409,
                "PLAN_VERSION_CONFLICT",
                $"Pass plan revision changed from {preCheckPlan.ActiveRevision} to {plan.ActiveRevision}; reload and verify financial terms before retrying.");
        }

        plan.SetEnabled(isEnabled, command.Request.ExpectedPlanRevision, actorId, clock.GetUtcNow());
        await repository.UpdatePlanAsync(plan, ct);
        await repository.SaveChangesAsync(ct);

        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        return new SetPassPlanEnablementResponseDto(
            plan.Id,
            plan.ProductType.ToString(),
            plan.ActiveRevision,
            plan.IsEnabled,
            plan.UpdatedAt);
    }
}
