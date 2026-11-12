using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.MarkOperationalBackup;

public sealed class MarkOperationalBackupCommandHandler(
    ISlotOperatorAuthorizer operators,
    IMediator mediator,
    ILogger<MarkOperationalBackupCommandHandler> logger)
    : IRequestHandler<MarkOperationalBackupCommand>
{
    public async Task Handle(MarkOperationalBackupCommand request, CancellationToken ct)
    {
        logger.LogInformation("Marking operational backup.");
        var operatorScope = await operators.ScopeAsync(request.SiteId, ct);
        await using var session = await mediator.Send(new BeginStructureEditCommand(operatorScope, request.SiteId, MarkedSlot: request.SlotId, Reason: request.Reason), ct);
        session.Structure.MarkBackup(request.SlotId, true);
        await session.CommitAsync(ct);
    }
}
