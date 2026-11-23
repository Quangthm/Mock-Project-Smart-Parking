using MediatR;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseGateSlot;

public sealed class ReleaseGateSlotCommandHandler(
    IOperatorGateSlotRepository repository,
    IUnitOfWork work,
    ILayoutInvalidationPublisher? invalidation = null)
    : IRequestHandler<ReleaseGateSlotCommand>
{
    public async Task Handle(ReleaseGateSlotCommand request, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var released = await repository.ReleaseAsync(request.TenantId, request.SiteId, request.SlotId, ct);
        if (released && invalidation is not null)
        {
            await invalidation.StageAsync(
                request.TenantId,
                request.SiteId,
                [LayoutInvalidationResources.FullLayout, LayoutInvalidationResources.CapacitySummary],
                ct);
        }
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
