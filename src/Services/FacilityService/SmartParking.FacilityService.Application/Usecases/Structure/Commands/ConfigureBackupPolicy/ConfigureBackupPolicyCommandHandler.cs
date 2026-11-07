using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.ConfigureBackupPolicy;

public sealed class ConfigureBackupPolicyCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IStructureUnitOfWork unitOfWork,
    ILogger<ConfigureBackupPolicyCommandHandler> logger)
    : IRequestHandler<ConfigureBackupPolicyCommand>
{
    public async Task Handle(ConfigureBackupPolicyCommand request, CancellationToken ct)
    {
        logger.LogInformation("Configuring backup policy for unit {UnitId} in site {SiteId}", request.UnitId, request.SiteId);
        // Requires BACKUP_CONFIGURE permission
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct, "BACKUP_CONFIGURE");
        await using var session = await unitOfWork.BeginEditAsync(owner, request.SiteId, ct);
        session.Structure.ConfigureBackup(
            request.UnitId == Guid.Empty ? null : request.UnitId,
            request.VehicleType ?? throw new StructureException("INVALID_STRUCTURE", "Vehicle type is required."),
            request.Capacity);
        await session.CommitAsync(ct);
    }
}
