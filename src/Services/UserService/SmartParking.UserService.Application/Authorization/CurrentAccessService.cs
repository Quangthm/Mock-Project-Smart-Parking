using Microsoft.Extensions.Logging;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Application.Authorization;

public sealed class CurrentAccessService(ICurrentAccessStore store, ICurrentActor actor,
    TimeProvider clock, ILogger<CurrentAccessService> logger) : ICurrentAccessService
{
    public Task<CurrentAccessContext> RequireAsync(CancellationToken cancellationToken) =>
        ReadAsync(actor, false, cancellationToken);

    public Task<CurrentAccessContext> LockAsync(CancellationToken cancellationToken) =>
        ReadAsync(actor, true, cancellationToken);

    public Task<CurrentAccessContext> ValidateAsync(ICurrentActor authenticatedActor, CancellationToken cancellationToken) =>
        ReadAsync(authenticatedActor, false, cancellationToken);

    private async Task<CurrentAccessContext> ReadAsync(ICurrentActor current, bool acquireLocks, CancellationToken cancellationToken)
    {
        try
        {
            var accountId = current.AccountId ?? throw AuthException.InvalidToken();
            var tokenId = current.AccessTokenId ?? throw AuthException.InvalidToken();
            var state = acquireLocks
                ? await store.LockCurrentAsync(accountId, tokenId, cancellationToken)
                : await store.LoadCurrentAsync(accountId, tokenId, cancellationToken);
            return CurrentAccessPolicy.RequireCurrentAccess(state, current, clock);
        }
        catch (AuthException exception)
        {
            logger.LogInformation("Current account access denied for {AccountId}: {Decision}", current.AccountId, exception.Code);
            throw;
        }
    }
}
