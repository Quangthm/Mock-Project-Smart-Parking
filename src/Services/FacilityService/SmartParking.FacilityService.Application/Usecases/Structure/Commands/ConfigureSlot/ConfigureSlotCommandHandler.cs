using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.ConfigureSlot;

public sealed class ConfigureSlotCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IStructureUnitOfWork unitOfWork,
    ILogger<ConfigureSlotCommandHandler> logger)
    : IRequestHandler<ConfigureSlotCommand>
{
    public async Task Handle(ConfigureSlotCommand request, CancellationToken ct)
    {
        logger.LogInformation("Configuring slot {SlotId} in site {SiteId}", request.SlotId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        var primary = SlotCompatibility.Primary(request.VehicleType, request.PrimaryVehicleType, request.VehicleTypes);
        await using var session = await unitOfWork.BeginEditAsync(owner, request.SiteId, ct);
        session.Structure.ConfigureSlot(
            request.SlotId, primary, request.SlotType, request.Coordinates3D, request.Features, request.VehicleTypes);
        await session.CommitAsync(ct);
    }
}
