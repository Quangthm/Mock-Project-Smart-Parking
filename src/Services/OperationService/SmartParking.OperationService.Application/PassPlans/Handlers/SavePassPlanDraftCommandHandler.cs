using MediatR;
using SmartParking.OperationService.Application.PassPlans.Commands;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Application.PassPlans.Services;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Handlers;

public sealed class SavePassPlanDraftCommandHandler(
    IPassPlanFacilityScopePort scopePort,
    IPassPlanRepository repository,
    TimeProvider clock)
    : IRequestHandler<SavePassPlanDraftCommand, SavePassPlanDraftResponseDto>
{
    public async Task<SavePassPlanDraftResponseDto> Handle(SavePassPlanDraftCommand command, CancellationToken ct)
    {
        var productType = PassPlanHelper.ParseProductType(command.ProductType);
        var req = command.Request;
        var vehicleTypes = (req.VehicleTypes ?? [])
            .Select(v => PassPlanHelper.ParseVehicleType(v, "vehicleTypes"))
            .Distinct()
            .OrderBy(v => v.ToString(), StringComparer.Ordinal)
            .ToArray();

        var excludedZoneIds = (req.OrdinaryCoverageRules?.ExcludedZoneIds ?? [])
            .Distinct()
            .ToArray();

        var scope = await scopePort.ValidateOwnerDraftScopeAsync(
            command.SiteId,
            req.ZoneId,
            vehicleTypes,
            excludedZoneIds,
            ct);

        var actorId = command.ActorUserId ?? scope.UserId;
        var draftSpec = new PassPlanDraftSpec(
            req.ZoneId,
            vehicleTypes,
            new OrdinaryCoverageRules(
                req.OrdinaryCoverageRules!.DayCovered,
                req.OrdinaryCoverageRules.NightCovered,
                excludedZoneIds),
            req.IsAvailableOnly == true);

        await using var tx = await repository.BeginSiteLockTransactionAsync(command.SiteId, ct);
        var now = clock.GetUtcNow();

        var existing = await repository.GetPlanAsync(command.SiteId, productType, ct);
        ParkingPassPlan plan;
        if (existing is null)
        {
            plan = ParkingPassPlan.CreateDraft(scope.TenantId, command.SiteId, productType, draftSpec, actorId, now);
            await repository.AddPlanAsync(plan, ct);
        }
        else
        {
            plan = existing;
            plan.SaveDraft(draftSpec, actorId, now);
            await repository.UpdatePlanAsync(plan, ct);
        }

        await repository.SaveChangesAsync(ct);
        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        return new SavePassPlanDraftResponseDto(
            plan.Id,
            plan.SiteId,
            plan.ProductType.ToString(),
            "DRAFT",
            plan.DraftRevision!.Value,
            plan.UpdatedAt);
    }
}
