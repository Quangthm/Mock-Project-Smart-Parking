using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseOperationalBackup;

public sealed class ReleaseOperationalBackupCommandHandler(
    ISlotOperatorAuthorizer operators,
    IMediator mediator,
    ILogger<ReleaseOperationalBackupCommandHandler> logger)
    : IRequestHandler<ReleaseOperationalBackupCommand>
{
    public async Task Handle(ReleaseOperationalBackupCommand request, CancellationToken ct)
    {
        logger.LogInformation("Releasing operational backup for slot {SlotId} at site {SiteId}",
            request.SlotId, request.SiteId);
        var operatorScope = await operators.ScopeAsync(request.SiteId, ct);
        const string reason = "Backup designation released by operator.";
        await using var session = await mediator.Send(new BeginStructureEditCommand(
            operatorScope, request.SiteId, MarkedSlot: request.SlotId, Reason: reason), ct);
        session.Structure.MarkBackup(request.SlotId, false);
        await session.CommitAsync(ct);
    }
}
