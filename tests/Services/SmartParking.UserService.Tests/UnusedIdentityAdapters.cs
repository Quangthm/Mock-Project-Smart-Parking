using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;

namespace SmartParking.UserService.Tests;

// Deliberately fail if unrelated HTTP slice tests accidentally call identity persistence.
internal class UnusedIdentityRepository : IIdentityAccessRepository
{
    public virtual Task<Guid[]> OwnerTenantsAsync(Guid actor, string permission, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<OperatorDto>> ListOperatorsAsync(Guid actor, Guid[] tenants, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<OperatorAssignment>> AssignmentsAsync(Guid actor, CancellationToken ct) => throw new NotSupportedException();
    public virtual Task<bool> EmailExistsAsync(string email, CancellationToken ct) => throw new NotSupportedException();
    public virtual void AddOperator(User user, IEnumerable<OperatorGrant> grants, AuthChallenge challenge, WorkflowDelivery delivery) => throw new NotSupportedException();
    public Task<(User User, AuthChallenge Challenge)?> LockInvitationAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task RevokeSessionsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
    public Task<User?> GetOperatorForUpdateAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<OperatorGrant>> GetOperatorGrantsAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<AuthChallenge>> GetPendingInvitationsAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task ReplaceInvitationAsync(Guid userId, AuthChallenge challenge, WorkflowDelivery delivery, CancellationToken ct) => throw new NotSupportedException();
}
internal class UnusedAccessEvents : IAccessEventStore
{
    public virtual void Stage(OperatorAccessChanged message) => throw new NotSupportedException();
    public Task<AccessEventLease?> ClaimAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task CompleteAsync(AccessEventLease lease, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> ApplyProjectionAsync(OperatorAccessChanged message, CancellationToken ct) => throw new NotSupportedException();
}
