using MediatR;
using Microsoft.Extensions.Logging;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseOperationalBackup;

public sealed class ReleaseOperationalBackupCommandHandler(
    ISlotOperatorAuthorizer operators,
    ParkingStructureService structures,
    ILogger<ReleaseOperationalBackupCommandHandler> logger)
    : IRequestHandler<ReleaseOperationalBackupCommand>
{
    public async Task Handle(ReleaseOperationalBackupCommand request, CancellationToken ct)
    {
        logger.LogInformation("Releasing operational backup for slot {SlotId} at site {SiteId}",
            request.SlotId, request.SiteId);
        var operatorScope = await operators.ScopeAsync(request.SiteId, ct);
        await structures.SetOperationalBackupAsync(
            operatorScope, request.SiteId, request.SlotId, false, "Backup designation released by operator.", ct);
    }
}
