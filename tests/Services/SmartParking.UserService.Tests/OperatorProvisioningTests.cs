using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Common.Models.JwT;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Application.Usecase.Login;
using UserService.Infrastructure.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class OperatorProvisioningTests
{
    [PostgresFact]
    public async Task RealSchemaProvisioningScopeRollbackConcurrencyAndLogin()
    {
        var input = Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var database = "smartpark_auth_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(input); await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin).ExecuteNonQueryAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(
            new NpgsqlConnectionStringBuilder(input) { Database = database }.ConnectionString).Options;
        var clock = new TestClock(); var passwords = new BcryptPasswordService();
        var ownerId = Guid.NewGuid(); var tenant = Guid.NewGuid(); var otherTenant = Guid.NewGuid();
        var site1 = Guid.NewGuid(); var site2 = Guid.NewGuid(); var foreignSite = Guid.NewGuid(); var inactiveSite = Guid.NewGuid();
        CreateOperatorDto Body(string email = " STAFF@PERSONAL.EXAMPLE ") => new() { FullName = " Staff Name ", Email = email,
            Password = "Password@123", SiteIds = [site1, site2], Permissions = ["DEVICE_MANAGE", "CASH_COLLECT"] };
        async Task<T> Run<T>(Func<OperatorProvisioningService, Task<T>> action)
        {
            await using var db = new AppDbContext(options);
            return await action(new(db, passwords, clock));
        }
        Task<OperatorDto> Create(CreateOperatorDto body, Guid? actor = null) => Run(s => s.CreateAsync(actor ?? ownerId, body, default));
        Task<bool> Allowed(Guid user, Guid site, string p) => Run(s => s.HasPermissionAsync(user, site, p, default));
        async Task Expect(string code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<AuthException>(action)).Code);
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "SmartParking.slnx"))) root = root.Parent;
            await using (var db = new AppDbContext(options))
            {
                await db.Database.OpenConnectionAsync();
                foreach (var file in new[] { "05.1-Database-Scripts.sql", "05.4-Driver-Registration-OTP.sql", "05.7-Operator-Provisioning.sql", "05.7-Operator-Provisioning.sql" })
                {
                    await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root!.FullName, "scripts/database", file)),
                        (NpgsqlConnection)db.Database.GetDbConnection());
                    await command.ExecuteNonQueryAsync();
                }
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO tenants(id,code,name) VALUES ({tenant},'owned','Owned'),({otherTenant},'foreign','Foreign')");
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO parking_sites(id,tenant_id,site_code,name,address,is_active) VALUES
                    ({site1},{tenant},'S1','First','Address',true),({site2},{tenant},'S2','Second','Address',true),
                    ({foreignSite},{otherTenant},'S3','Foreign','Address',true),({inactiveSite},{tenant},'S4','Inactive','Address',false)
                    """);
                db.Users.Add(new User { Id = ownerId, FullName = "Owner", Email = "owner@example.com", PasswordHash = passwords.Hash("Password@123"), Status = UserStatus.Active,
                    Accounts = [new Account { Id = Guid.NewGuid(), AccountType = "BUSINESS_OPERATOR", TenantId = tenant,
                        AccountRoles = [new AccountRole { RoleCode = "BUSINESS_OWNER" }] }] });
                await db.SaveChangesAsync();
            }
            await Expect("FORBIDDEN", () => Create(Body(), Guid.NewGuid()));
            foreach (var badSite in new[] { foreignSite, inactiveSite, Guid.NewGuid() })
            {
                var b = Body(); b.SiteIds = [site1, badSite];
                await Expect("SITE_ACCESS_DENIED", () => Create(b));
            }
            var invalid = Body(); invalid.Permissions = ["OPERATOR_MANAGE"];
            await Expect("VALIDATION_FAILED", () => Create(invalid));
            await using (var db = new AppDbContext(options))
            { Assert.Equal(1, await db.Users.CountAsync()); Assert.Empty(await db.OperatorGrants.ToListAsync()); }
            var op = await Create(Body());
            Assert.Equal("staff@personal.example", op.Email); Assert.Equal("Staff Name", op.FullName);
            Assert.Equal("operator", op.Role); Assert.Equal(ownerId, op.CreatedBy);
            await using (var db = new AppDbContext(options))
            {
                var user = await new UserRepository(db).GetByIdWithRolesAsync(op.Id, default);
                Assert.Equal(2, user!.Accounts.Count); Assert.Equal(UserStatus.Active, user.Status);
                Assert.True(passwords.Verify("Password@123", user.PasswordHash)); Assert.NotEqual("Password@123", user.PasswordHash);
                Assert.Null(user.Phone); Assert.Equal("operator", AccessTokenService.CurrentRole(user));
                Assert.All(user.Accounts, a => { Assert.Equal(tenant, a.TenantId); Assert.NotNull(a.SiteId); Assert.Equal("SITE_OPERATOR", Assert.Single(a.AccountRoles).RoleCode); });
                Assert.All(await db.OperatorGrants.ToListAsync(), g => Assert.Equal(ownerId, g.CreatedBy));
                var jwt = new JwtOptions { Issuer = "operator-test", Audience = "operator-test" };
                using var keys = new JwtKeyProvider(jwt, true); var sessions = new PostgresAuthSessionStore(db, clock);
                var tokens = new AccessTokenService(jwt, keys, sessions, clock); var policy = new AuthenticationPolicy();
                var login = new LoginCommandHandler(new UnitOfWork(db, new UserRepository(db)), passwords,
                    new AuthSessionService(tokens, sessions, policy, clock), policy, clock);
                Assert.True((await login.Handle(new() { Email = op.Email, Password = "Password@123" }, default)).IsSuccess);
            }
            Assert.True(await Allowed(op.Id, site1, "DEVICE_MANAGE")); Assert.True(await Allowed(op.Id, site2, "CASH_COLLECT"));
            Assert.False(await Allowed(op.Id, foreignSite, "DEVICE_MANAGE")); Assert.False(await Allowed(op.Id, site1, "APPEAL_REVIEW"));
            Assert.False(await Allowed(ownerId, site1, "DEVICE_MANAGE")); Assert.False(await Allowed(op.Id, site1, "REFUND_APPROVE"));
            await Expect("EMAIL_EXISTS", () => Create(Body("Staff@Personal.Example")));
            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            { try { await Create(Body("race@example.com")); return "created"; } catch (AuthException e) { return e.Code; } }));
            Assert.Single(results, r => r == "created"); Assert.Single(results, r => r == "EMAIL_EXISTS");
            await using (var db = new AppDbContext(options))
            {
                Assert.Equal(3, await db.Users.CountAsync()); Assert.Equal(5, await db.Accounts.CountAsync()); Assert.Equal(4, await db.OperatorGrants.CountAsync());
                await db.Users.Where(u => u.Id == op.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Locked));
            }
            Assert.False(await Allowed(op.Id, site1, "DEVICE_MANAGE"));
            await using (var db = new AppDbContext(options))
            {
                await db.Users.Where(u => u.Id == op.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Active));
                await db.Accounts.Where(a => a.UserId == op.Id && a.SiteId == site1).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, "SUSPENDED"));
            }
            Assert.False(await Allowed(op.Id, site1, "DEVICE_MANAGE")); Assert.True(await Allowed(op.Id, site2, "DEVICE_MANAGE"));
            await using (var db = new AppDbContext(options))
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE parking_sites SET is_active=false WHERE id={site2}");
            Assert.False(await Allowed(op.Id, site2, "DEVICE_MANAGE"));
            await using (var db = new AppDbContext(options))
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE parking_sites SET is_active=true WHERE id={site2}");
                await db.OperatorGrants.Where(g => g.Account.UserId == op.Id && g.Account.SiteId == site2)
                    .ExecuteUpdateAsync(s => s.SetProperty(g => g.Permissions, new[] { "CASH_COLLECT" }));
            }
            Assert.False(await Allowed(op.Id, site2, "DEVICE_MANAGE")); Assert.True(await Allowed(op.Id, site2, "CASH_COLLECT"));
            await using (var db = new AppDbContext(options))
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tenants SET status='SUSPENDED' WHERE id={tenant}");
            Assert.False(await Allowed(op.Id, site2, "CASH_COLLECT"));
            await Expect("FORBIDDEN", () => Create(Body("suspended@example.com")));
            await using (var db = new AppDbContext(options))
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tenants SET status='ACTIVE' WHERE id={tenant}");
            await using (var db = new AppDbContext(options))
                await db.Users.Where(u => u.Id == ownerId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.PendingApproval));
            await Expect("FORBIDDEN", () => Create(Body("pending@example.com")));
        }
        finally
        {
            Assert.Matches("^smartpark_auth_test_[0-9a-f]{32}$", database);
            await new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
        }
    }
}
