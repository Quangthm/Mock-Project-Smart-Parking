using MediatR;
using Microsoft.Extensions.Logging;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseGateSlot;

public sealed class ReleaseGateSlotCommandHandler(
    OperatorGateSlotService gateSlots,
    ILogger<ReleaseGateSlotCommandHandler> logger)
    : IRequestHandler<ReleaseGateSlotCommand>
{
    public async Task Handle(ReleaseGateSlotCommand request, CancellationToken ct)
    {
        logger.LogInformation("Releasing gate slot {SlotId} at site {SiteId} for tenant {TenantId}",
            request.SlotId, request.SiteId, request.TenantId);
        await gateSlots.ReleaseAsync(request.TenantId, request.SiteId, request.SlotId, ct);
    }
}
