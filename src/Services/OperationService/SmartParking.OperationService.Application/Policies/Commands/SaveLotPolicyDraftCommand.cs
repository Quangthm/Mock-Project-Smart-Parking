using MediatR;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Ports;
using SmartParking.OperationService.Domain.Policies;

namespace SmartParking.OperationService.Application.Policies.Commands;

public sealed record SaveLotPolicyDraftCommand(
    Guid SiteId,
    Guid? ActorId,
    SavePolicyDraftRequest Request
) : IRequest<SavePolicyDraftResponse>;

public sealed class SaveLotPolicyDraftCommandHandler(
    ILotPolicyRepository repository,
    ISiteOwnershipValidator ownershipValidator)
    : IRequestHandler<SaveLotPolicyDraftCommand, SavePolicyDraftResponse>
{
    public async Task<SavePolicyDraftResponse> Handle(SaveLotPolicyDraftCommand command, CancellationToken ct)
    {
        var tenantId = await ownershipValidator.ValidateOwnershipAsync(command.ActorId, command.SiteId, ct);

        var config = command.Request.ToDraftConfig();

        var saved = await repository.SaveDraftAsync(
            command.SiteId,
            tenantId,
            command.ActorId ?? Guid.Empty,
            config,
            command.Request.ScheduledActivationAt,
            ct
        );

        return new SavePolicyDraftResponse(
            saved.SiteId,
            "DRAFT",
            saved.TargetRevision,
            saved.UpdatedAt,
            "Draft policy configuration saved successfully."
        );
    }
}
