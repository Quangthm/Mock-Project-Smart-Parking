using MediatR;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Ports;

namespace SmartParking.OperationService.Application.Policies.Queries;

public sealed record GetLotPolicyCatalogueQuery(Guid SiteId, Guid? ActorId) : IRequest<PolicyCatalogueResponse>;

public sealed class GetLotPolicyCatalogueQueryHandler(
    ILotPolicyRepository repository,
    ISiteOwnershipValidator ownershipValidator)
    : IRequestHandler<GetLotPolicyCatalogueQuery, PolicyCatalogueResponse>
{
    public async Task<PolicyCatalogueResponse> Handle(GetLotPolicyCatalogueQuery request, CancellationToken ct)
    {
        await ownershipValidator.ValidateOwnershipAsync(request.ActorId, request.SiteId, ct);

        var data = await repository.GetCatalogueAsync(request.SiteId, ct);

        return new PolicyCatalogueResponse(
            data.SiteId,
            data.ActiveRevision,
            data.ActiveSince,
            data.HasDraft,
            data.Policies
        );
    }
}
