using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Profiles.Contracts;

namespace UserService.Application.Profiles.Queries;

/// <summary>Reads the caller's own profile.</summary>
public sealed record GetOwnProfileQuery : IRequest<OwnProfileResponse>;

public sealed class GetOwnProfileQueryHandler(IProfileAccessStore access, ICurrentActor actor, TimeProvider clock)
    : IRequestHandler<GetOwnProfileQuery, OwnProfileResponse>
{
    public async Task<OwnProfileResponse> Handle(GetOwnProfileQuery request, CancellationToken cancellationToken)
    {
        var accountId = actor.AccountId ?? throw AuthException.InvalidToken();
        var tokenId = actor.AccessTokenId ?? throw AuthException.InvalidToken();
        var state = await access.LoadCurrentAsync(accountId, tokenId, cancellationToken);
        var role = OwnProfileAccess.RequireCurrentAccess(state, actor, clock);
        return OwnProfileResponse.From(state!.User, role);
    }
}
