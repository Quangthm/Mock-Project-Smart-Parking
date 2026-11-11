using MediatR;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseGateSlot;

public sealed class ReleaseGateSlotCommandHandler(
    IOperatorGateSlotRepository repository,
    IUnitOfWork work)
    : IRequestHandler<ReleaseGateSlotCommand>
{
    public async Task Handle(ReleaseGateSlotCommand request, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        await repository.ReleaseAsync(request.TenantId, request.SiteId, request.SlotId, ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
