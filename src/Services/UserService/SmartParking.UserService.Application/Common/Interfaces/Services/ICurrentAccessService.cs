using UserService.Application.Authorization;

namespace UserService.Application.Common.Interfaces.Services;

public interface ICurrentAccessService
{
    Task<CurrentAccessContext> RequireAsync(CancellationToken cancellationToken);
    /// <summary>The caller retains its unit-of-work transaction through the protected write.</summary>
    Task<CurrentAccessContext> LockAsync(CancellationToken cancellationToken);
    /// <summary>Adapts an authenticated principal before the transport publishes its current actor.</summary>
    Task<CurrentAccessContext> ValidateAsync(ICurrentActor actor, CancellationToken cancellationToken);
}
