using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.MarkOperationalBackup;

public sealed class MarkOperationalBackupCommandHandler(
    ISlotOperatorAuthorizer operators,
    IStructureUnitOfWork unitOfWork,
    ILogger<MarkOperationalBackupCommandHandler> logger)
    : IRequestHandler<MarkOperationalBackupCommand>
{
    public async Task Handle(MarkOperationalBackupCommand request, CancellationToken ct)
    {
        logger.LogInformation("Marking operational backup for slot {SlotId} at site {SiteId} with reason: {Reason}",
            request.SlotId, request.SiteId, request.Reason);
        var operatorScope = await operators.ScopeAsync(request.SiteId, ct);
        await using var session = await unitOfWork.BeginEditAsync(
            operatorScope, request.SiteId, ct, markedSlot: request.SlotId, reason: request.Reason);
        session.Structure.MarkBackup(request.SlotId, true);
        await session.CommitAsync(ct);
    }
}
