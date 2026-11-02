using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Persistence;

/// <summary>Fresh persistence state for the exact account and access-token session.</summary>
public sealed record CurrentAccessState(
    User User,
    Account Account,
    AuthSession Session,
    string? RoleCode);

public interface ICurrentAccessStore
{
    /// <summary>Reads current state without populating the unit of work's identity map.</summary>
    Task<CurrentAccessState?> LoadCurrentAsync(Guid accountId, Guid accessTokenId, CancellationToken cancellationToken);

    /// <summary>
    /// Acquires authorization row locks and returns a fresh tracked User for a narrow mutation.
    /// The caller must retain its unit-of-work transaction through save and commit.
    /// </summary>
    Task<CurrentAccessState?> LockCurrentAsync(Guid accountId, Guid accessTokenId, CancellationToken cancellationToken);
}
