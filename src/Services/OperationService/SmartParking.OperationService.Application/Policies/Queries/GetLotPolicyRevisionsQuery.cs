using MediatR;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Ports;

namespace SmartParking.OperationService.Application.Policies.Queries;

public sealed record GetLotPolicyRevisionsQuery(
    Guid SiteId,
    Guid? ActorId,
    int Page = 1,
    int PageSize = 10
) : IRequest<PolicyRevisionsResponse>;

public sealed class GetLotPolicyRevisionsQueryHandler(
    ILotPolicyRepository repository,
    ISiteOwnershipValidator ownershipValidator)
    : IRequestHandler<GetLotPolicyRevisionsQuery, PolicyRevisionsResponse>
{
    public async Task<PolicyRevisionsResponse> Handle(GetLotPolicyRevisionsQuery request, CancellationToken ct)
    {
        await ownershipValidator.ValidateOwnershipAsync(request.ActorId, request.SiteId, ct);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : (request.PageSize > 50 ? 50 : request.PageSize);

        var paged = await repository.GetRevisionsAsync(request.SiteId, page, pageSize, ct);

        return new PolicyRevisionsResponse(
            request.SiteId,
            paged.TotalCount,
            paged.Items
        );
    }
}
