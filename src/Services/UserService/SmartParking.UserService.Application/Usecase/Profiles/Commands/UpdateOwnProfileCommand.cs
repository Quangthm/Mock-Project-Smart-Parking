using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Profiles.Events;
using SmartParking.UserService.Application.Usecase.Profiles.Contracts;

namespace SmartParking.UserService.Application.Usecase.Profiles.Commands;

/// <summary>Changes the caller's own display name.</summary>
public sealed record UpdateOwnProfileCommand(string FullName) : IRequest<OwnProfileResponse>;

public sealed class UpdateOwnProfileCommandHandler(IUnitOfWork unitOfWork, ICurrentAccessService access,
    ICurrentActor actor, IEventPublisher events, TimeProvider clock)
    : IRequestHandler<UpdateOwnProfileCommand, OwnProfileResponse>
{
    public async Task<OwnProfileResponse> Handle(UpdateOwnProfileCommand request, CancellationToken cancellationToken)
    {
        // Name-only policy accepts invalidation committed after this fresh check.
        var current = await access.RequireAsync(cancellationToken);
        var user = await unitOfWork.UserRepository.GetByIdForUpdateAsync(current.User.Id!.Value, cancellationToken)
            ?? throw global::UserService.Application.Common.Models.Exceptions.AuthException.InvalidToken();
        var previousName = user.FullName;
        user.UpdateFullName(request.FullName);
        if (!StringComparer.Ordinal.Equals(previousName, user.FullName))
        {
            await events.PublishAsync(new UserProfileNameChanged(Guid.NewGuid(), current.Account.Id!.Value,
                user.Id!.Value, clock.GetUtcNow(), actor.CorrelationId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return OwnProfileResponse.From(user, current.Role);
    }
}
