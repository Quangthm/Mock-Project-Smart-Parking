using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.MessageBroker;
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
    public Task<UserSimulatedIdentity?> FindSimulatedIdentityAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public void AddSimulatedIdentity(UserSimulatedIdentity identity) => throw new NotSupportedException();
}
internal class RecordingEventPublisher : IEventPublisher
{
    public List<IIntegrationEventMessage> Messages { get; } = [];
    public virtual Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, IIntegrationEventMessage
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public async Task PublishManyAsync<TMessage>(IEnumerable<TMessage> messages, CancellationToken cancellationToken)
        where TMessage : class, IIntegrationEventMessage
    {
        foreach (var message in messages)
            await PublishAsync(message, cancellationToken);
    }
}
