using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ClaimGateSlot;

public sealed class ClaimGateSlotCommandHandler(
    IOperatorGateSlotStore store,
    ILogger<ClaimGateSlotCommandHandler> logger)
    : IRequestHandler<ClaimGateSlotCommand, OperatorGateSlot>
{
    public async Task<OperatorGateSlot> Handle(ClaimGateSlotCommand request, CancellationToken ct)
    {
        logger.LogInformation("Claiming gate slot at site {SiteId} for tenant {TenantId} (VehicleType: {VehicleType})",
            request.SiteId, request.TenantId, request.VehicleType);
        return await store.ClaimAvailableAsync(
            request.TenantId, request.SiteId, request.VehicleType,
            request.PreferredSlotId, request.TargetUnitId, request.AllowReservedSlot, ct);
    }
}
