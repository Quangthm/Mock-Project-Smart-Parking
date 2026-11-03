using UserService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Services;
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
        directory.OnProvision = async (id, name, email, phone, ct) =>
        {
            await using var tenantDb = new AppDbContext(options);
            await tenantDb.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tenants (id, code, name, contact_email, contact_phone)
                VALUES ({id}, {"tenant-" + id.ToString("N")}, {name}, {email}, {phone})
                ON CONFLICT (id) DO NOTHING
                """, ct);
        };
        async Task<T> Run<T>(Func<OwnerRegistrationService, Task<T>> action)
        {
            await using var db = new AppDbContext(options);
            var authorization = await TestAuthorization.ForUserAsync(db, clock, adminId, directory);
            return await action(new(db, passwords, clock, directory, WorkflowTestSupport.Create(db,clock,authorization), authorization));
        }
        OwnerRegistrationDto Body(string email, string phone) => new() { FullName = "New Owner", BusinessName = "Company",
            Email = email, Phone = phone, Password = "Password@123", LotType = "basement", AgreedToPolicy = true };
        async Task<OwnerApplicationDto> Register(string email, string phone)
        {
            var result=await Run(s => s.RegisterAsync(Body(email, phone), default));
            Assert.Null(result.Verification);
            Assert.NotNull(result.SetupToken);
            await using (var before = new AppDbContext(options))
            {
                Assert.False(await before.AuthChallenges.AnyAsync(c => c.UserId == result.OwnerId));
                Assert.False(await before.WorkflowDeliveries.AnyAsync(c => c.UserId == result.OwnerId));
                var entity = await before.OwnerApplications.SingleAsync(a => a.Id == result.Id);
                Assert.NotEqual(result.SetupToken, entity.SetupTokenHash);
            }
            var setup = new OwnerLotSetupRequest { RegistrationId = result.Id, SetupToken = result.SetupToken!, Lot = new()
            { Name = "Test Lot", Address = "Test address", LotType = "outdoor", TotalSlots = 10, Floors = 1,
                ModelFileName = "outdoor_parking_lot.glb", PolicyAccepted = true } };
            await Expect("SETUP_TOKEN_INVALID", () => Run(s => s.SaveLotSetupAsync(new()
                { RegistrationId = setup.RegistrationId, SetupToken = "invalid", Lot = setup.Lot }, default)));
            var savedTime = clock.Now;
            clock.Now = clock.Now.AddDays(2);
            await Expect("SETUP_TOKEN_INVALID", () => Run(s => s.SaveLotSetupAsync(setup, default)));
            clock.Now = savedTime;
            setup.Lot.PolicyAccepted = false;
            await Expect("VALIDATION_FAILED", () => Run(s => s.SaveLotSetupAsync(setup, default)));
            setup.Lot.PolicyAccepted = true;
            result = await Run(s => s.SaveLotSetupAsync(setup, default));
            var retry = await Run(s => s.SaveLotSetupAsync(setup, default));
            Assert.Equal(result.Verification, retry.Verification);
            Assert.True(result.LotSetupComplete);
            Assert.Equal("outdoor", result.LotType);
            setup.Lot.Name = "Changed after submission";
            await Expect("SETUP_ALREADY_SAVED", () => Run(s => s.SaveLotSetupAsync(setup, default)));
            await using var db=new AppDbContext(options);
            foreach(var challenge in new[]{result.Verification!})
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
                // The schema already seeds PLATFORM_ADMIN; adding it again breaks the roles primary key.
                if (!await db.Roles.AnyAsync(r => r.Code == "PLATFORM_ADMIN"))
                    db.Roles.Add(new Role { Code = "PLATFORM_ADMIN", Name = "Admin" });
                db.Users.Add(new User { Id = adminId, FullName = "Admin", Phone = "0900000000", Email = "admin@example.com",
                    PasswordHash = passwords.Hash("Password@123"), Status = UserStatus.Active,
                    Accounts = [new Account { Id = Guid.NewGuid(), RoleCode = "PLATFORM_ADMIN" }] });
                await db.SaveChangesAsync();
                var root = new DirectoryInfo(AppContext.BaseDirectory);
                while (root != null && !File.Exists(Path.Combine(root.FullName, "SmartParking.slnx"))) root = root.Parent;
                // Prove upgrade creates a missing table and can safely run twice.
                await db.Database.ExecuteSqlRawAsync("DROP TABLE owner_applications");
                var migration = await File.ReadAllTextAsync(Path.Combine(root!.FullName, "scripts/database/05.5-Owner-Registration-Approval.sql"));
                await db.Database.ExecuteSqlRawAsync(migration);
                await db.Database.ExecuteSqlRawAsync(migration);
                var onboarding = await File.ReadAllTextAsync(ServiceSchema.FeaturePath("05.15-Owner-Onboarding.sql"));
                await db.Database.ExecuteSqlRawAsync(onboarding);
                await db.Database.ExecuteSqlRawAsync(onboarding);
            }
            var pending = await Register(" OWNER@EXAMPLE.COM ", "0911111111");
            Assert.Equal("pending", pending.Status);
            Assert.Equal("owner@example.com", pending.Email);
            await using (var db = new AppDbContext(options))
            {
                var user = await new UserRepository(db).GetByIdWithRolesAsync(pending.OwnerId, default);
                Assert.Equal(UserStatus.PendingApproval, user!.Status);
                Assert.Null(AccountRolePolicy.CurrentRole(user));
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
            directory.LoseProvisionResponse = true;
            await Expect("PARKING_UNAVAILABLE", () => Review(pending.Id, "approved"));
            Assert.True(directory.Sites.ContainsKey(pending.Id));
            await using (var incomplete = new AppDbContext(options))
            {
                Assert.Equal(UserStatus.PendingApproval, (await incomplete.Users.SingleAsync(u => u.Id == pending.OwnerId)).Status);
                Assert.False(await incomplete.WorkflowDeliveries.AnyAsync(d => d.UserId == pending.OwnerId && d.Kind == "OWNER_DECISION"));
            }
            directory.LoseProvisionResponse = false;
            Assert.Equal("pending", (await Run(s => s.ListAsync(adminId, default))).Single().Status);
            var decisions = await Task.WhenAll(new[] { "approved", "rejected" }.Select(async status =>
            {
                try { return (await Review(pending.Id, status)).Status; }
                catch (AuthException e) { return e.Code; }
            }));
            Assert.Single(decisions, d => d is "APPLICATION_CLOSED" or "APPROVAL_IN_PROGRESS");
            var final = (await Run(s => s.ListAsync(adminId, default))).Single();
            Assert.Equal(adminId, final.ReviewedBy);
            Assert.Equal(clock.Now, final.ReviewedAt);
            Assert.Equal("Reviewed", final.ReviewNote);
            Assert.Equal("approved", (await Review(pending.Id, "approved")).Status);
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
                Assert.Equal(status == "approved" ? "owner" : null, AccountRolePolicy.CurrentRole(user));
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
                if (result.IsSuccess)
                {
                    Assert.Equal("owner", result.Session!.User!.Role);
                    Assert.True(result.Session.User.OnboardingComplete);
                    Assert.True((await new global::UserService.Application.Usecase.Session.GetCurrentUserQueryHandler(new UnitOfWork(db, new UserRepository(db)))
                        .Handle(new(r.OwnerId), default))!.OnboardingComplete);
                }
            }
            var concurrent = await Register("concurrent-approval@example.com", "0966666666");
            var approvals = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                try { return (await Review(concurrent.Id, "approved")).Status; }
                catch (AuthException e) { return e.Code; }
            }));
            Assert.All(approvals, result => Assert.Equal("approved", result));
            await using (var check = new AppDbContext(options))
                Assert.Equal(1, await check.WorkflowDeliveries.CountAsync(d => d.UserId == concurrent.OwnerId && d.Kind == "OWNER_DECISION"));
            var legacy = await Run(s => s.RegisterAsync(Body("legacy@example.com", "0977777777"), default));
            await using (var old = new AppDbContext(options))
            {
                var workflow = WorkflowTestSupport.Create(old, clock);
                await Expect("SETUP_RECOVERY_REQUIRED", () => workflow.RecoverOwnerAsync(legacy.Email, default));
                var owner = await old.Users.SingleAsync(u => u.Id == legacy.OwnerId);
                var challenge = await workflow.IssueOwnerAsync(owner, default); // simulate pre-upgrade registration
                await Expect("LOT_SETUP_REQUIRED", () => workflow.VerifyOwnerAsync(challenge.ChallengeId, "000000", default));
                await Expect("LOT_SETUP_REQUIRED", () => workflow.ResendOwnerAsync(challenge.ChallengeId, default));
                owner.EmailVerifiedAt = clock.Now;
                owner.Status = UserStatus.PendingApproval;
                await old.SaveChangesAsync();
            }
            await Expect("LOT_SETUP_REQUIRED", () => Review(legacy.Id, "approved"));
            var adminList = await Run(s => s.ListAsync(adminId, default));
            Assert.All(adminList, item => Assert.Null(item.SetupToken));
            Assert.DoesNotContain("setupToken", System.Text.Json.JsonSerializer.Serialize(adminList, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
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
