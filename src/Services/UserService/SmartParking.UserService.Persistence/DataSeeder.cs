using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Validation;

namespace UserService.Persistence;

public class DataSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordService _passwords;

    public DataSeeder(AppDbContext dbContext, IPasswordService passwords)
    {
        _dbContext = dbContext;
        _passwords = passwords;
    }

    public async Task SeedAsync()
    {
        // Kiểm tra xem đã có user nào trong DB chưa
        if (await _dbContext.Users.AnyAsync())
        {
            return; // Đã có data, bỏ qua không seed nữa
        }

        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var accountId = Guid.NewGuid();

        var demoUser = new User
        {
            Id = userId,
            Email = "driver@gmail.com",
            Phone = "0987654321",
            FullName = "Demo Driver",
            PasswordHash = _passwords.Hash("Password@123"),
            Status = UserStatus.Active,
            CreatedOn = DateTimeOffset.UtcNow,
            ModifiedOn = DateTimeOffset.UtcNow
        };

        var demoAccount = new Account
        {
            Id = accountId,
            UserId = userId,
            Status = "ACTIVE",
            CreatedOn = DateTimeOffset.UtcNow,
            User = demoUser
        };

        var accountRole = new AccountRole
        {
            AccountId = accountId,
            RoleCode = "DRIVER",
            Account = demoAccount
        };

        if (!await _dbContext.Roles.AnyAsync(r => r.Code == "DRIVER"))
            _dbContext.Roles.Add(new Role { Code = "DRIVER", Name = "Driver" });

        demoAccount.AccountRoles.Add(accountRole);
        demoUser.Accounts.Add(demoAccount);

        _dbContext.Users.Add(demoUser);

        await _dbContext.SaveChangesAsync();
    }

    public async Task SeedRoleUserAsync(string email, string password, string fullName, string roleCode, string roleName)
    {
        if (!SignInContact.IsValidEmail(email) ||
            password.Length is < 8 or > 15 || !System.Text.RegularExpressions.Regex.IsMatch(password,
                @"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).+$"))
            throw new InvalidOperationException("Development bootstrap needs a valid email and an 8-15 character password with upper/lowercase, digit and special character.");
        email = email.Trim().ToLowerInvariant();
        var existing = await _dbContext.Users.Include(u => u.Accounts).ThenInclude(a => a.AccountRoles).AsSplitQuery()
            .SingleOrDefaultAsync(u => u.Email == email);
        if (existing is not null)
        {
            if (!existing.Accounts.Any(a => a.Status == "ACTIVE" && a.AccountRoles.Any(r => r.RoleCode == roleCode)))
                throw new InvalidOperationException("Development bootstrap email already belongs to a user with a different role.");
            return;
        }
        if (!await _dbContext.Roles.AnyAsync(r => r.Code == roleCode))
            _dbContext.Roles.Add(new Role { Code = roleCode, Name = roleName });
        _dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = fullName,
            PasswordHash = _passwords.Hash(password),
            Status = UserStatus.Active,
            CreatedOn = DateTimeOffset.UtcNow,
            ModifiedOn = DateTimeOffset.UtcNow,
            Accounts = [new Account { Id = Guid.NewGuid(), Status = "ACTIVE", CreatedOn = DateTimeOffset.UtcNow,
                AccountRoles = [new AccountRole { RoleCode = roleCode }] }]
        });
        await _dbContext.SaveChangesAsync();
    }
    public async Task SeedAdminAsync(string email, string password)
        => await SeedRoleUserAsync(email, password, "Development Administrator", "ADMIN", "Administrator");

}
