using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Application.Usecase.IdentityAccess;

public sealed record OperatorAssignmentsQuery(Guid Actor) : IRequest<IReadOnlyList<OperatorAssignment>>;
public sealed class OperatorAssignmentsHandler(IIdentityAccessRepository repository, IParkingDirectory parking)
    : IRequestHandler<OperatorAssignmentsQuery, IReadOnlyList<OperatorAssignment>>
{
    public async Task<IReadOnlyList<OperatorAssignment>> Handle(OperatorAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var assignments = await repository.AssignmentsAsync(request.Actor, cancellationToken);
        if (assignments.Count == 0) return [];
        var sites = await parking.ActiveSitesAsync(assignments.Select(assignment => assignment.SiteId).Distinct().ToArray(),
            assignments.Select(assignment => assignment.TenantId).Distinct().ToArray(), cancellationToken);
        return assignments.Where(assignment => sites.Any(site => site.Id == assignment.SiteId && site.TenantId == assignment.TenantId)).ToArray();
    }
}
