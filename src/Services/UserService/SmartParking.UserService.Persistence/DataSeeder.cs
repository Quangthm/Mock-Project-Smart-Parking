using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;

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
            AccountType = "DRIVER",
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
}
