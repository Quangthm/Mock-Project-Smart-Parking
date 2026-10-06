using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.API.Controllers;
using UserService.API.Middleware;
using UserService.Application;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Usecase.Login;
using UserService.Application.Usecase.Session;
using UserService.Infrastructure.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class UserManagementTests
{
    private sealed class FakeUsers : IUserManagementService
    {
        public int Calls;
        public Guid? AdminId;
        private readonly ManagedUserDto user = new(Guid.NewGuid(), "Driver", "driver@example.com", null,
            ["driver"], "active", DateTimeOffset.UtcNow, null);
        public Task<ManagedUsersPage> ListAsync(Guid adminId, UserSearchDto query, CancellationToken ct)
        { Calls++; AdminId = adminId; return Task.FromResult(new ManagedUsersPage([user], 1, query.Page, query.PageSize)); }
        public Task<ManagedUserDetail> GetAsync(Guid adminId, Guid id, CancellationToken ct)
        { Calls++; AdminId = adminId; return Task.FromResult(new ManagedUserDetail(user, [])); }
        public Task<ManagedUserDto> SetStatusAsync(Guid adminId, Guid id, UserStatusDto body, CancellationToken ct)
        { Calls++; AdminId = adminId; return Task.FromResult(user with { Status = body.Status }); }
    }

    [Fact]
    public async Task HttpAuthorizationValidationAndSecretFreeContract()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(UsersController).Assembly);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddJWTAuthentication(builder.Configuration, true);
        builder.Services.AddAuthorization();
        var fixture = new AuthTestFixture();
        builder.Services.AddSingleton<IUnitOfWork>(fixture);
        builder.Services.AddSingleton<IAuthSessionStore>(fixture.Sessions);
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        var users = new FakeUsers();
        builder.Services.AddSingleton<IUserManagementService>(users);
        await using var app = builder.Build();
        app.UseMiddleware<AuthExceptionMiddleware>();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var id = Guid.NewGuid();
        async Task Login()
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "driver@gmail.com", password = "Password@123" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            client.DefaultRequestHeaders.Authorization = new("Bearer", data.GetProperty("accessToken").GetString());
        }
        foreach (var path in new[] { "/api/users", $"/api/users/{id}" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PatchAsJsonAsync($"/api/users/{id}/status", new { status = "locked" })).StatusCode);
        await Login();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/users/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync($"/api/users/{id}/status", new { status = "locked" })).StatusCode);
        Assert.Equal(0, users.Calls);
        fixture.User.Accounts.Single().AccountRoles.Single().RoleCode = "PLATFORM_ADMIN";
        await Login();
        foreach (var query in new[] { "page=0", "pageSize=101", "role=superuser", "status=disabled", "page=oops" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/users?" + query)).StatusCode);
        foreach (var status in new[] { "", "disabled", "pendingApproval", "ACTIVE" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/users/{id}/status", new { status })).StatusCode);
        Assert.Equal(0, users.Calls);
        var list = await client.GetAsync("/api/users?role=driver&status=active&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("no-store", list.Headers.CacheControl!.ToString());
        var text = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/users/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/users/{id}/status", new { status = "locked" })).StatusCode);
        Assert.Equal(fixture.User.Id, users.AdminId);
        fixture.User.Status = UserStatus.Locked;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
        fixture.User.Status = UserStatus.Active;
        fixture.User.Accounts.Single().AccountRoles.Single().RoleCode = "BUSINESS_OWNER";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [PostgresFact]
    public async Task RealDatabaseSearchTransitionsAndSessionRevocation()
    {
        var input = Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var database = "smartpark_auth_test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(input);
        await connection.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection).ExecuteNonQueryAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(input) { Database = database }.ConnectionString).Options;
        var clock = new TestClock();
        var adminId = Guid.NewGuid(); var pendingId = Guid.NewGuid(); var deletedId = Guid.NewGuid(); Guid driverId;
        var tokenId = Guid.NewGuid();
        async Task<LoginResult> LoginDriver()
        {
            await using var db = new AppDbContext(options);
            var jwt = new JwtOptions { Issuer = "users-test", Audience = "users-test" };
            using var keys = new JwtKeyProvider(jwt, true);
            var sessions = new PostgresAuthSessionStore(db, clock);
            var policy = new AuthenticationPolicy();
            var tokens = new AccessTokenService(jwt, keys, sessions, clock);
            var handler = new LoginCommandHandler(new UnitOfWork(db, new UserRepository(db)), new BcryptPasswordService(),
                new AuthSessionService(tokens, sessions, policy, clock), policy, clock);
            return await handler.Handle(new() { Email = "driver@gmail.com", Password = "Password@123" }, default);
        }
        async Task RefreshOldSession()
        {
            await using var db = new AppDbContext(options);
            var sessions = new PostgresAuthSessionStore(db, clock);
            var handler = new RefreshCommandHandler(new UnitOfWork(db, new UserRepository(db)), sessions, null!, clock);
            await handler.Handle(new("test-refresh"), default);
        }
        async Task<T> Run<T>(Func<UserManagementService, Task<T>> action)
        {
            await using var db = new AppDbContext(options);
            return await action(new(db, clock));
        }
        Task<ManagedUserDto> Change(Guid actor, Guid target, string status) => Run(s => s.SetStatusAsync(actor, target, new() { Status = status }, default));
        async Task Expect(string code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<AuthException>(action)).Code);
        try
        {
            await using (var db = new AppDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                await new DataSeeder(db, new BcryptPasswordService()).SeedAsync();
                driverId = (await db.Users.SingleAsync()).Id!.Value;
                db.Roles.Add(new Role { Code = "PLATFORM_ADMIN", Name = "Admin" });
                db.Users.AddRange(new User { Id = adminId, FullName = "Admin", Status = UserStatus.Active,
                    Accounts = [new Account { Id = Guid.NewGuid(), AccountType = "PLATFORM_STAFF", AccountRoles = [new AccountRole { RoleCode = "PLATFORM_ADMIN" }] }] },
                    new User { Id = pendingId, FullName = "Pending", Status = UserStatus.PendingApproval },
                    new User { Id = deletedId, FullName = "Deleted", Status = UserStatus.Active, DeletedOn = clock.Now });
                db.AuthSessions.Add(new AuthSession { Id = Guid.NewGuid(), UserId = driverId, AccessTokenId = tokenId,
                    TokenHash = AuthSessionService.HashRefreshToken("test-refresh"), CreatedAt = clock.Now, AccessExpiresAt = clock.Now.AddHours(1), ExpiresAt = clock.Now.AddDays(7) });
                await db.SaveChangesAsync();
            }
            await Expect("FORBIDDEN", () => Run(s => s.ListAsync(driverId, new(), default)));
            await Expect("FORBIDDEN", () => Change(driverId, driverId, "locked"));
            await Expect("VALIDATION_FAILED", () => Run(s => s.ListAsync(adminId, new() { PageSize = 101 }, default)));
            await Expect("VALIDATION_FAILED", () => Change(adminId, driverId, "disabled"));
            var page = await Run(s => s.ListAsync(adminId, new() { Role = "driver", Search = " DRIVER@GMAIL.COM " }, default));
            Assert.Equal(driverId, Assert.Single(page.Items).Id);
            Assert.Equal(1, page.Total);
            Assert.Empty((await Run(s => s.ListAsync(adminId, new() { Role = "owner" }, default))).Items);
            Assert.Equal(pendingId, Assert.Single((await Run(s => s.ListAsync(adminId, new() { Status = "pendingApproval" }, default))).Items).Id);
            Assert.Equal(3, (await Run(s => s.ListAsync(adminId, new(), default))).Total);
            Assert.Empty((await Run(s => s.ListAsync(adminId, new() { Page = int.MaxValue, PageSize = 100 }, default))).Items);
            var first = await Run(s => s.ListAsync(adminId, new() { PageSize = 1 }, default));
            var second = await Run(s => s.ListAsync(adminId, new() { PageSize = 1, Page = 2 }, default));
            Assert.NotEqual(first.Items.Single().Id, second.Items.Single().Id);
            await Expect("USER_NOT_FOUND", () => Run(s => s.GetAsync(adminId, deletedId, default)));
            await Expect("USER_NOT_FOUND", () => Change(adminId, Guid.NewGuid(), "locked"));
            await Expect("ADMIN_ACCOUNT_PROTECTED", () => Change(adminId, adminId, "locked"));
            await Expect("INVALID_STATUS_TRANSITION", () => Change(adminId, pendingId, "active"));
            await Expect("INVALID_STATUS_TRANSITION", () => Change(adminId, pendingId, "locked"));
            Assert.False(Assert.Single((await Run(s => s.GetAsync(adminId, driverId, default))).LoginActivity).IsRevoked);
            var locked = await Change(adminId, driverId, "locked");
            Assert.Equal("locked", locked.Status); Assert.Null(locked.LockedUntil);
            Assert.False((await LoginDriver()).IsSuccess);
            await Expect("INVALID_TOKEN", RefreshOldSession);
            Assert.True(Assert.Single((await Run(s => s.GetAsync(adminId, driverId, default))).LoginActivity).IsRevoked);
            await using (var db = new AppDbContext(options))
            {
                Assert.False(await new PostgresAuthSessionStore(db, clock).IsActiveAsync(tokenId, driverId, default));
                Assert.True((await db.AuthSessions.SingleAsync()).IsRevoked);
            }
            Assert.Equal("active", (await Change(adminId, driverId, "active")).Status);
            await Expect("INVALID_TOKEN", RefreshOldSession);
            await using (var db = new AppDbContext(options))
            {
                Assert.False(await new PostgresAuthSessionStore(db, clock).IsActiveAsync(tokenId, driverId, default));
                var user = await db.Users.SingleAsync(u => u.Id == driverId);
                Assert.Equal(0, user.FailedLoginAttempts); Assert.Null(user.LockedUntil);
                user.Status = UserStatus.Locked; user.LockedUntil = clock.Now.AddMinutes(15); user.FailedLoginAttempts = 3;
                await db.SaveChangesAsync();
            }
            Assert.Equal("active", (await Change(adminId, driverId, "active")).Status);
            Assert.True((await LoginDriver()).IsSuccess);
            await Task.WhenAll(Change(adminId, driverId, "locked"), Change(adminId, driverId, "active"));
            await using (var db = new AppDbContext(options))
                await db.Users.Where(u => u.Id == adminId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Locked));
            await Expect("FORBIDDEN", () => Change(adminId, driverId, "active"));
        }
        finally
        {
            Assert.Matches("^smartpark_auth_test_[0-9a-f]{32}$", database);
            await new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", connection).ExecuteNonQueryAsync();
        }
    }
}
