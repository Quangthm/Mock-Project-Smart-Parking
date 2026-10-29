using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using UserService.Infrastructure.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class DirectRolePersistenceTests
{
    [Fact]
    public void ModelUsesRequiredRoleCodeWithSharedRoleAndRestrictedDeletion()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only").Options);
        var account = db.Model.FindEntityType(typeof(Account))!;
        var role = db.Model.FindEntityType(typeof(Role))!;
        var property = account.FindProperty(nameof(Account.RoleCode))!;
        var relationship = Assert.Single(account.GetForeignKeys(), f => f.PrincipalEntityType == role);

        Assert.Equal(nameof(Role.Code), Assert.Single(role.FindPrimaryKey()!.Properties).Name);
        Assert.False(property.IsNullable);
        Assert.Equal(50, property.GetMaxLength());
        Assert.True(relationship.IsRequired);
        Assert.False(relationship.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
        Assert.Equal(property, Assert.Single(relationship.Properties));
        var index = Assert.Single(account.GetIndexes(), i => i.GetDatabaseName() == "idx_accounts_role_code");
        Assert.False(index.IsUnique);
        Assert.DoesNotContain(db.Model.GetEntityTypes(), e => e.GetTableName() == "account_roles");
        Assert.DoesNotContain("account_roles", db.Accounts.Include(a => a.Role).ToQueryString());
    }

    [PostgresFact]
    public async Task CanonicalSetupSeedsAndEnforcesDirectRolesWithoutLegacyOverlays()
    {
        var input = Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var database = "smartpark_auth_test_" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnectionStringBuilder(input) { Database = database };
        await using var admin = new NpgsqlConnection(input);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connection.ConnectionString).Options);
            // This test deliberately checks only the original clean-install schema.
            // Session binding and outbox upgrades retain their separate deployment sequence.
            var sql = await File.ReadAllTextAsync(Path.Combine(ServiceSchema.Root,
                "scripts/database/microservices/01-user-access-service-db.sql"));
            await db.Database.ExecuteSqlRawAsync(sql);

            var seeder = new DataSeeder(db, new BcryptPasswordService());
            await seeder.SeedAsync();
            await seeder.SeedAdminAsync("admin@roles.test", "Fixture@123");
            await seeder.SeedAdminAsync("admin@roles.test", "Fixture@123");
            await seeder.SeedRoleUserAsync("owner@roles.test", "Fixture@123", "Owner", "BUSINESS_OWNER", "Business Owner");
            await seeder.SeedRoleUserAsync("operator@roles.test", "Fixture@123", "Operator", "SITE_OPERATOR", "Site Operator");
            db.ChangeTracker.Clear();

            Assert.Equal(7, await db.Roles.CountAsync());
            var accounts = await db.Accounts.Include(a => a.Role).Include(a => a.User).ToListAsync();
            Assert.Equal(4, accounts.Count);
            Assert.All(accounts, a => Assert.Equal(a.RoleCode, a.Role.Code));
            Assert.Equal("ADMIN", Assert.Single(accounts, a => a.User.Email == "admin@roles.test").Role.Code);
            Assert.Single(accounts, a => a.RoleCode == "ADMIN");
            Assert.Equal("BUSINESS_OWNER", Assert.Single(accounts, a => a.User.Email == "owner@roles.test").RoleCode);
            Assert.Equal("SITE_OPERATOR", Assert.Single(accounts, a => a.User.Email == "operator@roles.test").RoleCode);
            var driver = await new UserRepository(db).GetByEmailAsync("driver@gmail.com", default);
            Assert.Equal("DRIVER", Assert.Single(driver!.Accounts).Role.Code);
            var driverId = driver.Id!.Value;

            db.Accounts.Add(new Account { Id = Guid.NewGuid(), UserId = driverId, RoleCode = "DRIVER",
                CreatedOn = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.Accounts.CountAsync(a => a.RoleCode == "DRIVER"));

            await using var inspect = new NpgsqlConnection(connection.ConnectionString);
            await inspect.OpenAsync();
            async Task<object?> Scalar(string statement)
            {
                await using var command = new NpgsqlCommand(statement, inspect);
                return await command.ExecuteScalarAsync();
            }
            Assert.Equal(true, await Scalar("SELECT to_regclass('public.account_roles') IS NULL"));
            Assert.Equal("NO:50", await Scalar("SELECT is_nullable || ':' || character_maximum_length FROM information_schema.columns WHERE table_schema='public' AND table_name='accounts' AND column_name='role_code'"));
            Assert.Equal(false, await Scalar("SELECT i.indisunique FROM pg_index i JOIN pg_class c ON c.oid=i.indexrelid WHERE c.relname='idx_accounts_role_code'"));
            Assert.Equal("r", await Scalar("SELECT confdeltype::text FROM pg_constraint WHERE conrelid='accounts'::regclass AND confrelid='roles'::regclass AND contype='f'"));

            async Task Reject(string statement, string state)
            {
                await using var command = new NpgsqlCommand(statement, inspect);
                if (statement.Contains("@user")) command.Parameters.AddWithValue("user", driverId);
                var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
                Assert.Equal(state, error.SqlState);
            }
            await Reject("INSERT INTO accounts(user_id,role_code) VALUES (@user,NULL)", PostgresErrorCodes.NotNullViolation);
            await Reject("INSERT INTO accounts(user_id,role_code) VALUES (@user,'UNKNOWN')", PostgresErrorCodes.ForeignKeyViolation);
            await Reject("DELETE FROM roles WHERE code='DRIVER'", PostgresErrorCodes.ForeignKeyViolation);
        }
        finally
        {
            // Cleanup is restricted to this generated disposable test database.
            Assert.Matches("^smartpark_auth_test_[0-9a-f]{32}$", database);
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
            using var pool = new NpgsqlConnection(connection.ConnectionString);
            NpgsqlConnection.ClearPool(pool);
        }
    }
}
