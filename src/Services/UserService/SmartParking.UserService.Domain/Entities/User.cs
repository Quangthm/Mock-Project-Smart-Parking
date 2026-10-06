using SmartParking.UserService.Domain.Enum;
using UserService.Domain.Base;
using System.Collections.Generic;

namespace SmartParking.UserService.Domain.Entities;

public class User : BaseEntity
{
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public UserStatus Status { get; set; }

    public ICollection<Account> Accounts { get; set; } = [];
}