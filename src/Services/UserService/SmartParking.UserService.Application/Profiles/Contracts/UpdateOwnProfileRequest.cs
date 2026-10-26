namespace UserService.Application.Profiles.Contracts;

/// <summary>
/// The permitted client input for an own-profile update.
/// </summary>
/// <remarks>
/// Only the display name is accepted. Contact details, credentials, role, account status
/// and permissions are not part of this contract: contact changes use the re-verification
/// flow, passwords use the change-password flow, and the rest are never customer-editable.
/// </remarks>
public sealed class UpdateOwnProfileRequest
{
    /// <summary>The new display name.</summary>
    public string? FullName { get; set; }
}
