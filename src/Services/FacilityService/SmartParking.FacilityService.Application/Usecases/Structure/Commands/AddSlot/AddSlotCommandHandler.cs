using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddSlot;

public sealed class AddSlotCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<AddSlotCommandHandler> logger)
    : IRequestHandler<AddSlotCommand, Guid>
{
    public async Task<Guid> Handle(AddSlotCommand request, CancellationToken ct)
    {
        logger.LogInformation("Adding slot {SlotCode} to site {SiteId}", request.Code, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        return await structureService.AddSlotAsync(
            owner, request.SiteId, request.UnitId, request.Code, request.VehicleType,
            request.Type, request.VehicleTypes, request.PrimaryVehicleType, ct);
    }
}
