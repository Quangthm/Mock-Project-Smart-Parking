using MediatR;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Ports;
using SmartParking.OperationService.Domain.Policies;

namespace SmartParking.OperationService.Application.Policies.Queries;

public sealed record GetLotPolicyRevisionSnapshotQuery(
    Guid SiteId,
    int Version,
    Guid? ActorId
) : IRequest<PolicySnapshotResponse>;

public sealed class GetLotPolicyRevisionSnapshotQueryHandler(
    ILotPolicyRepository repository,
    ISiteOwnershipValidator ownershipValidator)
    : IRequestHandler<GetLotPolicyRevisionSnapshotQuery, PolicySnapshotResponse>
{
    public async Task<PolicySnapshotResponse> Handle(GetLotPolicyRevisionSnapshotQuery request, CancellationToken ct)
    {
        await ownershipValidator.ValidateOwnershipAsync(request.ActorId, request.SiteId, ct);

        var snapshot = await repository.GetRevisionSnapshotAsync(request.SiteId, request.Version, ct);

        if (snapshot is null)
        {
            throw new LotPolicyNotFoundException("REVISION_NOT_FOUND", "Policy revision not found.");
        }

        return new PolicySnapshotResponse(
            snapshot.SiteId,
            snapshot.RevisionNumber,
            snapshot.Status,
            snapshot.EffectiveFrom,
            snapshot.EffectiveTo,
            snapshot.PublishedBy,
            snapshot.Policies
        );
    }
}
