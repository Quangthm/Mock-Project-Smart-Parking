using MediatR;
using Microsoft.Extensions.Logging;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.MarkOperationalBackup;

public sealed class MarkOperationalBackupCommandHandler(
    ISlotOperatorAuthorizer operators,
    ParkingStructureService structures,
    ILogger<MarkOperationalBackupCommandHandler> logger)
    : IRequestHandler<MarkOperationalBackupCommand>
{
    public async Task Handle(MarkOperationalBackupCommand request, CancellationToken ct)
    {
        logger.LogInformation("Marking operational backup for slot {SlotId} at site {SiteId} with reason: {Reason}",
            request.SlotId, request.SiteId, request.Reason);
        var operatorScope = await operators.ScopeAsync(request.SiteId, ct);
        await structures.MarkOperationalBackupAsync(operatorScope, request.SiteId, request.SlotId, request.Reason, ct);
    }
}
