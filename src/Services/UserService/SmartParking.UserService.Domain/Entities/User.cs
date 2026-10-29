using SmartParking.UserService.Domain.Enum;
using SmartParking.UserService.Domain.ValueObjects;
using UserService.Domain.Base;
using System.Collections.Generic;

namespace SmartParking.UserService.Domain.Entities;

public class User : BaseEntity
{
    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public bool OnboardingComplete { get; set; }
    public long AccessVersion { get; set; }
    public string? CompanyName { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public DateTimeOffset? PhoneVerifiedAt { get; set; }

    /// <summary>
    /// The existing stored avatar representation, preserved verbatim on profile reads.
    /// </summary>
    /// <remarks>
    /// Profile input cannot assign or remove this value. Media mutation is a separate slice.
    /// </remarks>
    public string? AvatarUrl { get; set; }

    public UserStatus Status { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public ICollection<Account> Accounts { get; set; } = [];

    /// <summary>
    /// Applies a self-service display-name change.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not an acceptable display name.</exception>
    public void UpdateFullName(string fullName)
    {
        if (!ProfileName.IsValid(fullName))
            throw new ArgumentException("The display name is not acceptable.", nameof(fullName));
        FullName = ProfileName.Normalize(fullName);
    }

}
