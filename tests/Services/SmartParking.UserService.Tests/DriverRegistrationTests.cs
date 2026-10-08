using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Infrastructure.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;
using UserService.Application.Services;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Usecase.Login;

namespace SmartParking.UserService.Tests;

public sealed class DriverRegistrationTests
{
    private sealed class Sender : IOtpSender
    {
        public string Code { get; private set; } = "";
        public bool Fail { get; set; }
        public Task SendAsync(string channel, string destination, string code, CancellationToken ct)
        {
            if (Fail) throw new AuthException("OTP_DELIVERY_FAILED", "Simulated delivery failure", 503);
            Code = code;
            return Task.CompletedTask;
        }
    }

    [PostgresFact]
    public async Task RegistrationLifecycleConcurrencyAndDeliveryRollback()
    {
        var input = Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var database = "smartpark_auth_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(input);
        await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin).ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(input) { Database = database };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options;
        var clock = new TestClock();
        var sender = new Sender();
        var passwords = new BcryptPasswordService();
        async Task<T> Run<T>(Func<DriverRegistrationService, Task<T>> action)
        {
            await using var db = new AppDbContext(options);
            return await action(new(db, passwords, sender, clock));
        }
        Task<DriverRegistrationResult> Register(string email) => Run(s => s.RegisterAsync(
            new() { FullName = "New Driver", Email = email, Password = "Password@123" }, default));
        Task Verify(Guid id, string code) => Run(async s => { await s.VerifyAsync(new() { RegistrationId = id, Code = code }, default); return true; });
        async Task<LoginResult> Login(string contact)
        {
            await using var db = new AppDbContext(options);
            var jwt = new JwtOptions { Issuer = "registration-test", Audience = "registration-test" };
            using var keys = new JwtKeyProvider(jwt, true);
            var store = new PostgresAuthSessionStore(db, clock);
            var tokens = new AccessTokenService(jwt, keys, store, clock);
            var policy = new AuthenticationPolicy();
            return await new LoginCommandHandler(new UnitOfWork(db, new UserRepository(db)), passwords,
                new AuthSessionService(tokens, store, policy, clock), policy, clock)
                .Handle(new LoginCommand { Email = contact, Password = "Password@123" }, default);
        }
        async Task Expect(string code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<AuthException>(action)).Code);
        try
        {
            await using (var db = new AppDbContext(options))
            {
                await ServiceSchema.InitializeAsync(db);
                await new DataSeeder(db, passwords).SeedAsync();
                // Exercise upgrading the old phone-required schema, and repeated migration application.
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE users ALTER COLUMN phone SET NOT NULL");
                var root = new DirectoryInfo(AppContext.BaseDirectory);
                while (root != null && !File.Exists(Path.Combine(root.FullName, "SmartParking.slnx"))) root = root.Parent;
                var migration = await File.ReadAllTextAsync(Path.Combine(root!.FullName, "scripts/database/05.4-Driver-Registration-OTP.sql"));
                await db.Database.ExecuteSqlRawAsync(migration);
                await db.Database.ExecuteSqlRawAsync(migration);
            }
            var r = await Register(" DRIVER@EXAMPLE.COM ");
            var firstCode = sender.Code;
            Assert.False((await Login("driver@example.com")).IsSuccess);
            Assert.Equal(clock.Now.AddMinutes(5), r.ExpiresAt);
            await using (var db = new AppDbContext(options))
            {
                var user = await new UserRepository(db).GetByEmailForLoginAsync("driver@example.com", default);
                Assert.Equal(UserStatus.PendingVerification, user!.Status);
                Assert.Null(user.Phone);
                Assert.True(passwords.Verify("Password@123", user.PasswordHash));
                var challenge = await db.DriverRegistrations.SingleAsync();
                Assert.NotEqual(firstCode, challenge.CodeHash);
                Assert.True(passwords.Verify(firstCode, challenge.CodeHash));
            }
            await Expect("CONTACT_EXISTS", () => Register("driver@example.com"));
            await Expect("OTP_COOLDOWN", () => Run(s => s.ResendAsync(r.RegistrationId, default)));
            await Expect("OTP_INVALID", () => Verify(r.RegistrationId, "wrong"));
            clock.Now = clock.Now.AddSeconds(60);
            await Run(s => s.ResendAsync(r.RegistrationId, default));
            Assert.NotEqual(firstCode, sender.Code);
            var validCode = sender.Code;
            // Old code is rejected; resend preserved the previous failed attempt.
            await Expect("OTP_INVALID", () => Verify(r.RegistrationId, firstCode));
            await Expect("OTP_LOCKED", () => Verify(r.RegistrationId, "wrong"));
            await Expect("OTP_LOCKED", () => Verify(r.RegistrationId, validCode));
            await Expect("OTP_LOCKED", () => Run(s => s.ResendAsync(r.RegistrationId, default)));
            clock.Now = clock.Now.AddMinutes(15);
            await Expect("OTP_EXPIRED", () => Verify(r.RegistrationId, validCode));
            await Run(s => s.ResendAsync(r.RegistrationId, default));
            await Verify(r.RegistrationId, sender.Code);
            Assert.True((await Login("DRIVER@example.com")).IsSuccess);
            await Expect("REGISTRATION_CLOSED", () => Verify(r.RegistrationId, sender.Code));
            await using (var db = new AppDbContext(options))
            {
                var user = await new UserRepository(db).GetByEmailForLoginAsync("DRIVER@example.com", default);
                Assert.Equal(UserStatus.Active, user!.Status);
                Assert.Equal("DRIVER", user.Accounts.Single().AccountRoles.Single().RoleCode);
            }
            var parallel = await Register("parallel@example.com");
            var attempts = await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
            {
                try { await Verify(parallel.RegistrationId, "wrong"); return "success"; }
                catch (AuthException e) { return e.Code; }
            }));
            Assert.Equal(2, attempts.Count(c => c == "OTP_INVALID"));
            Assert.Single(attempts, c => c == "OTP_LOCKED");
            sender.Fail = true;
            await Expect("OTP_DELIVERY_FAILED", () => Register("rollback@example.com"));
            await using (var db = new AppDbContext(options))
                Assert.False(await db.Users.AnyAsync(u => u.Email == "rollback@example.com"));
            sender.Fail = false;
            var phone = await Run(s => s.RegisterAsync(new() { FullName = "Phone Driver", Phone = "0901234567", Password = "Password@123" }, default));
            Assert.Equal("sms", phone.Channel);
            await Verify(phone.RegistrationId, sender.Code);
            Assert.True((await Login("0901234567")).IsSuccess);
            await using (var db = new AppDbContext(options))
                Assert.Equal(UserStatus.Active, (await new UserRepository(db).GetByEmailForLoginAsync("0901234567", default))!.Status);
            var duplicates = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                try { await Register("duplicate@example.com"); return "created"; }
                catch (AuthException e) { return e.Code; }
            }));
            Assert.Single(duplicates, c => c == "created");
            Assert.Single(duplicates, c => c == "CONTACT_EXISTS");
        }
        finally
        {
            Assert.Matches("^smartpark_auth_test_[0-9a-f]{32}$", database);
            await new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
        }
    }
}
