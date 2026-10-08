using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Infrastructure.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class OwnerRegistrationTests
{
    [PostgresFact]
    public async Task RegistrationReviewDuplicatesConcurrencyAndMigration()
    {
        var input = Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var database = "smartpark_auth_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(input);
        await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin).ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(input) { Database = database };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options;
        var clock = new TestClock();
        clock.Now = new DateTimeOffset(clock.Now.Ticks - clock.Now.Ticks % 10, TimeSpan.Zero);
        var passwords = new BcryptPasswordService();
        var adminId = Guid.NewGuid();
        var directory = new TestParkingDirectory();
        async Task<T> Run<T>(Func<OwnerRegistrationService, Task<T>> action)
        {
            await using var db = new AppDbContext(options);
            return await action(new(db, passwords, clock, directory, WorkflowTestSupport.Create(db,clock)));
        }
        OwnerRegistrationDto Body(string email, string phone) => new() { FullName = "New Owner", BusinessName = "Company",
            Email = email, Phone = phone, Password = "Password@123", LotType = "basement", AgreedToPolicy = true };
        async Task<OwnerApplicationDto> Register(string email, string phone)
        {
            var result=await Run(s => s.RegisterAsync(Body(email, phone), default));
            await using var db=new AppDbContext(options);
            foreach(var challenge in new[]{result.Verification!,result.PhoneVerification!})
            {
                var delivery=await db.WorkflowDeliveries.SingleAsync(d=>d.ChallengeId==challenge.ChallengeId);
                var message=System.Text.Json.JsonSerializer.Deserialize<global::UserService.Application.Common.Interfaces.Services.WorkflowMessage>(new TestWorkflowProtector().Unprotect(delivery.ProtectedPayload))!;
                await WorkflowTestSupport.Create(db,clock).VerifyOwnerAsync(challenge.ChallengeId,System.Text.RegularExpressions.Regex.Match(message.Body,@"\d{6}").Value,default);
            }
            return result;
        }
        Task<OwnerApplicationDto> Review(Guid id, string status) => Run(s => s.ReviewAsync(adminId, id,
            new() { Status = status, ReviewNote = " Reviewed " }, default));
        async Task Expect(string code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<AuthException>(action)).Code);
        try
        {
            await using (var db = new AppDbContext(options))
            {
                await ServiceSchema.InitializeAsync(db);
                await new DataSeeder(db, passwords).SeedAsync();
                db.Roles.Add(new Role { Code = "PLATFORM_ADMIN", Name = "Admin" });
                db.Users.Add(new User { Id = adminId, FullName = "Admin", Phone = "0900000000", Email = "admin@example.com",
                    PasswordHash = passwords.Hash("Password@123"), Status = UserStatus.Active,
                    Accounts = [new Account { Id = Guid.NewGuid(), AccountRoles = [new AccountRole { RoleCode = "PLATFORM_ADMIN" }] }] });
                await db.SaveChangesAsync();
                var root = new DirectoryInfo(AppContext.BaseDirectory);
                while (root != null && !File.Exists(Path.Combine(root.FullName, "SmartParking.slnx"))) root = root.Parent;
                // Prove upgrade creates a missing table and can safely run twice.
                await db.Database.ExecuteSqlRawAsync("DROP TABLE owner_applications");
                var migration = await File.ReadAllTextAsync(Path.Combine(root!.FullName, "scripts/database/05.5-Owner-Registration-Approval.sql"));
                await db.Database.ExecuteSqlRawAsync(migration);
                await db.Database.ExecuteSqlRawAsync(migration);
            }
            var pending = await Register(" OWNER@EXAMPLE.COM ", "0911111111");
            Assert.Equal("pending", pending.Status);
            Assert.Equal("owner@example.com", pending.Email);
            await using (var db = new AppDbContext(options))
            {
                var user = await new UserRepository(db).GetByIdWithRolesAsync(pending.OwnerId, default);
                Assert.Equal(UserStatus.PendingApproval, user!.Status);
                Assert.Null(AccessTokenService.CurrentRole(user));
                Assert.True(passwords.Verify("Password@123", user.PasswordHash));
                var jwt = new global::UserService.Application.Common.Models.JwT.JwtOptions { Issuer = "owner-test", Audience = "owner-test" };
                using var keys = new JwtKeyProvider(jwt, true);
                var sessions = new PostgresAuthSessionStore(db, clock);
                var tokens = new AccessTokenService(jwt, keys, sessions, clock);
                var policy = new global::UserService.Application.Common.Models.JwT.AuthenticationPolicy();
                var login = new global::UserService.Application.Usecase.Login.LoginCommandHandler(new UnitOfWork(db, new UserRepository(db)),
                    passwords, new AuthSessionService(tokens, sessions, policy, clock), policy, clock);
                Assert.False((await login.Handle(new() { Email = pending.Email, Password = "Password@123" }, default)).IsSuccess);
            }
            await Expect("CONTACT_EXISTS", () => Register("owner@example.com", "0922222222"));
            await Expect("CONTACT_EXISTS", () => Register("another@example.com", "0911111111"));
            await Expect("FORBIDDEN", () => Run(s => s.ListAsync(pending.OwnerId, default)));
            await Expect("FORBIDDEN", () => Run(s => s.ReviewAsync(pending.OwnerId, pending.Id, new() { Status = "approved" }, default)));
            await Expect("APPLICATION_NOT_FOUND", () => Review(Guid.NewGuid(), "approved"));
            directory.Unavailable = true;
            await Expect("PARKING_UNAVAILABLE", () => Review(pending.Id, "approved"));
            directory.Unavailable = false;
            Assert.Equal("pending", (await Run(s => s.ListAsync(adminId, default))).Single().Status);
            var decisions = await Task.WhenAll(new[] { "approved", "rejected" }.Select(async status =>
            {
                try { return (await Review(pending.Id, status)).Status; }
                catch (AuthException e) { return e.Code; }
            }));
            Assert.Single(decisions, d => d == "APPLICATION_CLOSED");
            var final = (await Run(s => s.ListAsync(adminId, default))).Single();
            Assert.Equal(adminId, final.ReviewedBy);
            Assert.Equal(clock.Now, final.ReviewedAt);
            Assert.Equal("Reviewed", final.ReviewNote);
            await Expect("APPLICATION_CLOSED", () => Review(pending.Id, "approved"));
            var duplicates = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                try { await Register("concurrent@example.com", "0955555555"); return "created"; }
                catch (AuthException e) { return e.Code; }
            }));
            Assert.Single(duplicates, d => d == "created");
            Assert.Single(duplicates, d => d == "CONTACT_EXISTS");
            foreach (var status in new[] { "approved", "rejected" })
            {
                var r = await Register(status + "@example.com", status == "approved" ? "0933333333" : "0944444444");
                await Review(r.Id, status);
                await using var db = new AppDbContext(options);
                var user = await new UserRepository(db).GetByIdWithRolesAsync(r.OwnerId, default);
                Assert.Equal(status == "approved" ? UserStatus.Active : UserStatus.Rejected, user!.Status);
                Assert.Equal(status == "approved" ? "owner" : null, AccessTokenService.CurrentRole(user));
                if (status == "approved") { Assert.Equal(r.Id, user.Accounts.Single().TenantId); Assert.Contains(r.Id, directory.Provisioned); }
                var jwt = new global::UserService.Application.Common.Models.JwT.JwtOptions { Issuer = "owner-test", Audience = "owner-test" };
                using var keys = new JwtKeyProvider(jwt, true);
                var sessions = new PostgresAuthSessionStore(db, clock);
                var tokens = new AccessTokenService(jwt, keys, sessions, clock);
                var policy = new global::UserService.Application.Common.Models.JwT.AuthenticationPolicy();
                var login = new global::UserService.Application.Usecase.Login.LoginCommandHandler(new UnitOfWork(db, new UserRepository(db)),
                    passwords, new AuthSessionService(tokens, sessions, policy, clock), policy, clock);
                var result = await login.Handle(new() { Email = r.Email, Password = "Password@123" }, default);
                Assert.Equal(status == "approved", result.IsSuccess);
                if (result.IsSuccess) Assert.Equal("owner", result.Session!.User!.Role);
            }
            await using (var db = new AppDbContext(options))
                await db.Users.Where(u => u.Id == adminId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Locked));
            await Expect("FORBIDDEN", () => Run(s => s.ListAsync(adminId, default)));
            var invalid = Body("bad@example.com", "0955555555");
            invalid.AgreedToPolicy = false;
            await Expect("VALIDATION_FAILED", () => Run(s => s.RegisterAsync(invalid, default)));
        }
        finally
        {
            Assert.Matches("^smartpark_auth_test_[0-9a-f]{32}$", database);
            await new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
        }
    }
}
