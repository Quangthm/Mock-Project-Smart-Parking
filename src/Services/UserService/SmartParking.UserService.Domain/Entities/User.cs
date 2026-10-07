using SmartParking.UserService.Domain.Enum;
using UserService.Domain.Base;
using System.Collections.Generic;

namespace SmartParking.UserService.Domain.Entities;

public class User : BaseEntity
{
    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }

    public UserStatus Status { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public ICollection<Account> Accounts { get; set; } = [];
}
