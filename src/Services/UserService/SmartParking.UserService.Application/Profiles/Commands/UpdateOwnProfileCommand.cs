using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Profiles.Contracts;
using UserService.Application.Profiles.Events;

namespace UserService.Application.Profiles.Commands;

/// <summary>Changes the caller's own display name.</summary>
public sealed record UpdateOwnProfileCommand(string FullName) : IRequest<OwnProfileResponse>;

public sealed class UpdateOwnProfileCommandHandler(IUnitOfWork unitOfWork, IProfileAccessStore access,
    ICurrentActor actor, IEventPublisher events, TimeProvider clock)
    : IRequestHandler<UpdateOwnProfileCommand, OwnProfileResponse>
{
    public async Task<OwnProfileResponse> Handle(UpdateOwnProfileCommand request, CancellationToken cancellationToken)
    {
        var accountId = actor.AccountId ?? throw AuthException.InvalidToken();
        var tokenId = actor.AccessTokenId ?? throw AuthException.InvalidToken();
        // Keep the authorization guard until both the narrow name write and durable event
        // capture commit. The publisher is scoped to this unit of work's EF Bus Outbox.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var state = await access.LockCurrentAsync(accountId, tokenId, cancellationToken);
        var role = OwnProfileAccess.RequireCurrentAccess(state, actor, clock);
        var user = state!.User;
        var previousName = user.FullName;
        user.UpdateFullName(request.FullName);
        if (!StringComparer.Ordinal.Equals(previousName, user.FullName))
        {
            await events.PublishAsync(new UserProfileNameChanged(Guid.NewGuid(), accountId,
                user.Id!.Value, clock.GetUtcNow(), actor.CorrelationId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return OwnProfileResponse.From(user, role);
    }
}
