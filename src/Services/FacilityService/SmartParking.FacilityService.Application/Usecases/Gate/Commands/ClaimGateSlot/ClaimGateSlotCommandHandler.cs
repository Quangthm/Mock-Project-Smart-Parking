using MediatR;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ClaimGateSlot;

public sealed class ClaimGateSlotCommandHandler(
    IOperatorGateSlotRepository repository,
    IUnitOfWork work)
    : IRequestHandler<ClaimGateSlotCommand, OperatorGateSlot>
{
    public async Task<OperatorGateSlot> Handle(ClaimGateSlotCommand request, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var slot = await repository.ClaimAvailableAsync(
            request.TenantId, request.SiteId, request.VehicleType,
            request.PreferredSlotId, request.TargetUnitId, request.AllowReservedSlot, ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return slot;
    }
}
