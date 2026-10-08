using System.ComponentModel.DataAnnotations;

namespace UserService.Application.DTOs;

public sealed class UserSearchDto
{
    [StringLength(200)] public string? Search { get; set; }
    [RegularExpression("^(driver|owner|operator|admin)$")] public string? Role { get; set; }
    [RegularExpression("^(active|locked|pendingVerification|pendingApproval|rejected)$")] public string? Status { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public sealed class UserStatusDto
{
    [Required, RegularExpression("^(active|locked)$")] public string Status { get; set; } = "";
}

public sealed record ManagedUserDto(Guid Id, string FullName, string? Email, string? Phone,
    string[] Roles, string Status, DateTimeOffset? CreatedAt, DateTimeOffset? LockedUntil);
public sealed record ManagedUsersPage(IReadOnlyList<ManagedUserDto> Items, int Total, int Page, int PageSize);
public sealed record UserLoginActivity(DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, bool IsRevoked);
public sealed record ManagedUserDetail(ManagedUserDto User, IReadOnlyList<UserLoginActivity> LoginActivity);
