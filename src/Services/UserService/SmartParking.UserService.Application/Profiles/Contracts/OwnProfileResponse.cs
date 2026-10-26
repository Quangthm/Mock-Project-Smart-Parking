using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Profiles.Contracts;

/// <summary>
/// The profile data the caller is allowed to read about their own account.
/// </summary>
/// <remarks>
/// Role describes the exact authorized Account and is read-only. Status and permission
/// controls are not input fields. AvatarUrl preserves the existing stored value.
/// </remarks>
public sealed record OwnProfileResponse(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    string? AvatarUrl)
{
    public static OwnProfileResponse From(User user, string role) => new(
        user.Id!.Value,
        user.FullName,
        user.Email ?? string.Empty,
        role,
        user.AvatarUrl);
}
