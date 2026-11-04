using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.ConfigureBackupPolicy;

public sealed class ConfigureBackupPolicyCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<ConfigureBackupPolicyCommandHandler> logger)
    : IRequestHandler<ConfigureBackupPolicyCommand>
{
    public async Task Handle(ConfigureBackupPolicyCommand request, CancellationToken ct)
    {
        logger.LogInformation("Configuring backup policy for unit {UnitId} in site {SiteId}", request.UnitId, request.SiteId);
        // Requires BACKUP_CONFIGURE permission
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct, "BACKUP_CONFIGURE");
        await structureService.ChangeAsync(owner, request.SiteId,
            structure => structure.ConfigureBackup(request.UnitId == Guid.Empty ? null : request.UnitId,
                request.VehicleType ?? throw new StructureException("INVALID_STRUCTURE", "Vehicle type is required."), request.Capacity), ct);
    }
}
