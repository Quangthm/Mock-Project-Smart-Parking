using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Ports;
using SmartParking.OperationService.Domain.Policies;

namespace SmartParking.OperationService.Application.Policies.Queries;

public sealed record GetLotPolicyDraftQuery(Guid SiteId, Guid? ActorId) : IRequest<PolicyDraftResponse?>;

public sealed class GetLotPolicyDraftQueryHandler(
    ILotPolicyRepository repository,
    ISiteOwnershipValidator ownershipValidator)
    : IRequestHandler<GetLotPolicyDraftQuery, PolicyDraftResponse?>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PolicyDraftResponse?> Handle(GetLotPolicyDraftQuery request, CancellationToken ct)
    {
        await ownershipValidator.ValidateOwnershipAsync(request.ActorId, request.SiteId, ct);

        var draft = await repository.GetDraftAsync(request.SiteId, ct);

        if (draft is null)
        {
            return null;
        }

        PolicyDraftConfig config;
        try
        {
            config = JsonSerializer.Deserialize<PolicyDraftConfig>(draft.PayloadJson, JsonOptions) ?? new PolicyDraftConfig();
        }
        catch
        {
            config = new PolicyDraftConfig();
        }

        return new PolicyDraftResponse(
            draft.SiteId,
            "DRAFT",
            draft.TargetRevision,
            draft.UpdatedAt,
            draft.ScheduledActivationAt,
            config
        );
    }
}
