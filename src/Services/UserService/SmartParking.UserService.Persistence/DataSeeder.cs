using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;

namespace UserService.Persistence;

public class DataSeeder
{
    private readonly AppDbContext _dbContext;

    public DataSeeder(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedAsync()
    {
        // Kiểm tra xem đã có user nào trong DB chưa
        if (await _dbContext.Users.AnyAsync())
        {
            return; // Đã có data, bỏ qua không seed nữa
        }

        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var demoUser = new User
        {
            Id = userId,
            Email = "driver@gmail.com",
            Phone = "0987654321",
            FullName = "Demo Driver",
            PasswordHash = "Password@123", // Giữ nguyên plain-text để test
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

        demoAccount.AccountRoles.Add(accountRole);
        demoUser.Accounts.Add(demoAccount);

        _dbContext.Users.Add(demoUser);

        await _dbContext.SaveChangesAsync();
    }
}
