using SmartParking.UserService.Domain.Entities;
using UserService.Application.DTOs;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Application.Common.Interfaces.Persistence;

public interface IIdentityAccessRepository
{
    Task<Guid[]> OwnerTenantsAsync(Guid actor, string permission, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorDto>> ListOperatorsAsync(Guid actor, Guid[] tenants, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorAssignment>> AssignmentsAsync(Guid actor, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    void AddOperator(User user, IEnumerable<OperatorGrant> grants, AuthChallenge challenge, WorkflowDelivery delivery);
    Task<(User User, AuthChallenge Challenge)?> LockInvitationAsync(Guid id, CancellationToken cancellationToken);
    Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<User?> GetOperatorForUpdateAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorGrant>> GetOperatorGrantsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuthChallenge>> GetPendingInvitationsAsync(Guid userId, CancellationToken cancellationToken);
    Task ReplaceInvitationAsync(Guid userId, AuthChallenge challenge, WorkflowDelivery delivery, CancellationToken cancellationToken);
}
