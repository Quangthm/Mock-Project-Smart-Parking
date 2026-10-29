namespace UserService.Application.Common.Interfaces.Persistence;

/// <summary>Fresh identity-owned membership facts; the Application layer decides their authority.</summary>
public interface IAuthorizationScopeStore
{
    Task<IReadOnlyList<AccountMembership>> LoadAsync(Guid userId, bool lockRows, CancellationToken ct);
}

public sealed record AccountMembership(Guid AccountId, Guid UserId, string RoleCode, string Status,
    DateTimeOffset? DeletedAt, Guid? TenantId, Guid? SiteId, string[]? Permissions, string[]? OperatorPermissions);
