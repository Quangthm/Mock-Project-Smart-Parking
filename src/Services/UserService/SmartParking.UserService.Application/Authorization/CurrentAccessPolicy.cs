using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Application.Authorization;

/// <summary>
/// Shared current-account and session rules, independent of operation permissions and resource scope.
/// </summary>
/// <remarks>
/// A valid access token is not evidence that the account may still act: the account may have
/// been disabled, deleted or stripped of its role since the token was issued. The check is
/// therefore made against the authoritative record, never against the token claims.
/// </remarks>
public static class CurrentAccessPolicy
{
    public static AuthException Forbidden() => new("FORBIDDEN", "Current account access is required.", 403);

    public static CurrentAccessContext RequireCurrentAccess(CurrentAccessState? state, ICurrentActor actor, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        if (state is null || actor.UserId is null || actor.AccountId is null || actor.AccessTokenId is null
            || state.Session.AccountId != actor.AccountId || state.Session.UserId != actor.UserId
            || state.Session.AccessTokenId != actor.AccessTokenId || state.Session.IsRevoked
            || state.Session.AccessExpiresAt <= now || state.Session.ExpiresAt <= now)
            throw AuthException.InvalidToken();

        if (state.Account.Id != actor.AccountId || state.Account.UserId != actor.UserId
            || state.User.Id != state.Account.UserId || state.User.DeletedOn is not null
            || state.User.Status != UserStatus.Active || state.User.LockedUntil > now
            || state.Account.DeletedOn is not null || state.Account.Status != "ACTIVE")
            throw Forbidden();

        // Operational permissions and resource scope are checked separately by each use case.
        if (state.RoleCode != state.Account.RoleCode) throw Forbidden();
        var role = AccessRoles.Map(state.RoleCode) ?? throw Forbidden();
        if (actor.Role != role) throw Forbidden();
        return new(state.User, state.Account, state.Session, state.RoleCode!, role);
    }
}
