using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Application.Authorization;

public sealed record PublicationEvidenceQuery(Guid SiteId) : IRequest<PublicationEvidence>;
public sealed record PublicationEvidence(Guid SiteId, Guid TenantId, string LotType);

public sealed class PublicationEvidenceHandler(IAccountAuthorizationService authorization, IIdentityWorkflowStore store)
    : IRequestHandler<PublicationEvidenceQuery, PublicationEvidence>
{
    public async Task<PublicationEvidence> Handle(PublicationEvidenceQuery request, CancellationToken ct)
    {
        var scope = await authorization.OwnerScopeAsync(OwnerScopeAction.ParkingManage, ct);
        var decision = await store.FindOwnerApplicationByUserAsync(scope.UserId, ct);
        // Initial provisioning passes application.Id as both tenant and site. No such binding is
        // established for an additional site merely because its tenant belongs to this Owner.
        if (decision is not { Status: "approved", ReviewedAt: not null, ReviewedBy: not null, LotSetupJson: not null } ||
            decision.UserId != scope.UserId || request.SiteId != decision.Id || !scope.TenantIds.Contains(decision.Id))
            throw new AuthException("RESOURCE_NOT_FOUND", "Publication approval was not found.", 404);
        var lotType = decision.LotType switch
        {
            "multi-storey" => "multi_storey",
            "outdoor" or "basement" => decision.LotType,
            _ => throw new AuthException("RESOURCE_NOT_FOUND", "Publication approval was not found.", 404)
        };
        return new(decision.Id, decision.Id, lotType);
    }
}
