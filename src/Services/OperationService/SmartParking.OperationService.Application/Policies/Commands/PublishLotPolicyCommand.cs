using MediatR;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Ports;

namespace SmartParking.OperationService.Application.Policies.Commands;

public sealed record PublishLotPolicyCommand(
    Guid SiteId,
    Guid? ActorId,
    string? IdempotencyKey,
    PublishPolicyRequest Request
) : IRequest<PublishPolicyResponse>;

public sealed class PublishLotPolicyCommandHandler(
    ILotPolicyRepository repository,
    ISiteOwnershipValidator ownershipValidator)
    : IRequestHandler<PublishLotPolicyCommand, PublishPolicyResponse>
{
    public async Task<PublishPolicyResponse> Handle(PublishLotPolicyCommand command, CancellationToken ct)
    {
        var tenantId = await ownershipValidator.ValidateOwnershipAsync(command.ActorId, command.SiteId, ct);

        var result = await repository.PublishDraftAsync(
            command.SiteId,
            tenantId,
            command.ActorId ?? Guid.Empty,
            command.Request.Mode,
            command.Request.ScheduledAt,
            command.Request.PublishNote,
            command.IdempotencyKey,
            ct
        );

        return new PublishPolicyResponse(
            result.SiteId,
            result.Status,
            result.NewRevision,
            result.EffectiveFrom,
            result.PublishNote,
            result.IsReplayed
        );
    }
}
