using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Profiles.Contracts;
using UserService.Application.Profiles.Events;

namespace UserService.Application.Profiles.Commands;

/// <summary>Changes the caller's own display name.</summary>
public sealed record UpdateOwnProfileCommand(string FullName) : IRequest<OwnProfileResponse>;

public sealed class UpdateOwnProfileCommandHandler(IUnitOfWork unitOfWork, ICurrentAccessService access,
    ICurrentActor actor, IEventPublisher events, TimeProvider clock)
    : IRequestHandler<UpdateOwnProfileCommand, OwnProfileResponse>
{
    public async Task<OwnProfileResponse> Handle(UpdateOwnProfileCommand request, CancellationToken cancellationToken)
    {
        // Keep the authorization guard until both the narrow name write and durable event
        // capture commit. The publisher is scoped to this unit of work's EF Bus Outbox.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var current = await access.LockAsync(cancellationToken);
        var user = current.User;
        var previousName = user.FullName;
        user.UpdateFullName(request.FullName);
        if (!StringComparer.Ordinal.Equals(previousName, user.FullName))
        {
            await events.PublishAsync(new UserProfileNameChanged(Guid.NewGuid(), current.Account.Id!.Value,
                user.Id!.Value, clock.GetUtcNow(), actor.CorrelationId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return OwnProfileResponse.From(user, current.Role);
    }
}
