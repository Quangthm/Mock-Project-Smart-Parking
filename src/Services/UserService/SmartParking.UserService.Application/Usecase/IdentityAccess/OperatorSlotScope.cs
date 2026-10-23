using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Application.Usecase.IdentityAccess;

public sealed record OperatorSlotScopeQuery(Guid Actor, Guid SiteId) : IRequest<OwnerScope>;
public sealed class OperatorSlotScopeHandler(IIdentityAccessRepository repository, IParkingDirectory parking)
    : IRequestHandler<OperatorSlotScopeQuery, OwnerScope>
{
    public async Task<OwnerScope> Handle(OperatorSlotScopeQuery request, CancellationToken cancellationToken)
    {
        var assignment = (await repository.AssignmentsAsync(request.Actor, cancellationToken))
            .SingleOrDefault(assignment => assignment.SiteId == request.SiteId && assignment.Permissions.Contains("SLOT_OVERRIDE"));
        if (assignment is null)
            throw new AuthException("FORBIDDEN", "Current site permission is required.", 403);
        var sites = await parking.ActiveSitesAsync([assignment.SiteId], [assignment.TenantId], cancellationToken);
        if (!sites.Any(site => site.Id == assignment.SiteId && site.TenantId == assignment.TenantId))
            throw new AuthException("FORBIDDEN", "Current site permission is required.", 403);
        return new(request.Actor, [assignment.TenantId]);
    }
}
