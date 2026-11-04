using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddSlot;

public sealed class AddSlotCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IStructureUnitOfWork unitOfWork,
    ILogger<AddSlotCommandHandler> logger)
    : IRequestHandler<AddSlotCommand, Guid>
{
    public async Task<Guid> Handle(AddSlotCommand request, CancellationToken ct)
    {
        logger.LogInformation("Adding slot {SlotCode} to unit {UnitId} at site {SiteId}",
            request.Code, request.UnitId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await using var session = await unitOfWork.BeginEditAsync(owner, request.SiteId, ct);

        var compatibility = SlotCompatibility.Primary(
            request.VehicleType, request.PrimaryVehicleType, request.VehicleTypes);
        var slotId = session.Structure.AddSlot(
            request.UnitId, request.Code, compatibility, request.Type, request.VehicleTypes);

        await session.CommitAsync(ct);
        return slotId;
    }
}
