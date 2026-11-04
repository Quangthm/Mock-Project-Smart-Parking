using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.MarkBackup;

public sealed class MarkBackupCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<MarkBackupCommandHandler> logger)
    : IRequestHandler<MarkBackupCommand>
{
    public async Task Handle(MarkBackupCommand request, CancellationToken ct)
    {
        logger.LogInformation("Marking backup for slot {SlotId} in site {SiteId} (Active: {Active})",
            request.SlotId, request.SiteId, request.Active);
        // Explicit BACKUP_MARK check (H-2 / DEC-LOT-04)
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct, "BACKUP_MARK");
        await structureService.ChangeAsync(
            owner, request.SiteId, aggregate => aggregate.MarkBackup(request.SlotId, request.Active), ct);
    }
}
