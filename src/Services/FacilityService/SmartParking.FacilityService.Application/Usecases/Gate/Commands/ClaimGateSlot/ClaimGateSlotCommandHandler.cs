using MediatR;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ClaimGateSlot;

public sealed class ClaimGateSlotCommandHandler(
    IOperatorGateSlotRepository repository,
    IUnitOfWork work,
    ILayoutInvalidationPublisher? invalidation = null)
    : IRequestHandler<ClaimGateSlotCommand, OperatorGateSlot>
{
    public async Task<OperatorGateSlot> Handle(ClaimGateSlotCommand request, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var slot = await repository.ClaimAvailableAsync(
            request.TenantId, request.SiteId, request.VehicleType,
            request.PreferredSlotId, request.TargetUnitId, request.AllowReservedSlot, ct);
        if (invalidation is not null)
        {
            await invalidation.StageAsync(
                request.TenantId,
                request.SiteId,
                [LayoutInvalidationResources.FullLayout, LayoutInvalidationResources.CapacitySummary],
                ct);
        }
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return slot;
    }
}
