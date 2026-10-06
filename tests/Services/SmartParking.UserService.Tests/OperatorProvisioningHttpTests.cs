using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UserService.API.Controllers;
using UserService.API.Middleware;
using UserService.Application;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class OperatorProvisioningHttpTests
{
    private sealed class Operators : IOperatorProvisioningService
    {
        public int Calls;
        public Guid OwnerId;
        public Task<OperatorDto> CreateAsync(Guid ownerId, CreateOperatorDto body, CancellationToken ct)
        {
            Calls++; OwnerId = ownerId;
            return Task.FromResult(new OperatorDto(Guid.NewGuid(), body.FullName, body.Email,
                "operator", "active", ownerId, body.SiteIds, body.Permissions));
        }
        public Task<bool> HasPermissionAsync(Guid operatorId, Guid siteId, string permission, CancellationToken ct) => Task.FromResult(false);
    }

    [Fact]
    public async Task OwnerOnlyValidationAndSecretFreeResponse()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(UsersController).Assembly);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddJWTAuthentication(builder.Configuration, true); builder.Services.AddAuthorization();
        var fixture = new AuthTestFixture(); var operators = new Operators();
        builder.Services.AddSingleton<IUnitOfWork>(fixture);
        builder.Services.AddSingleton<IAuthSessionStore>(fixture.Sessions);
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        builder.Services.AddSingleton<IOperatorProvisioningService>(operators);
        // Create only uses the action-level provisioning service.
        builder.Services.AddSingleton<IUserManagementService, UnusedUsers>();
        await using var app = builder.Build();
        app.UseMiddleware<AuthExceptionMiddleware>(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync(); using var client = app.GetTestClient();
        CreateOperatorDto Body() => new() { FullName = "Operator", Email = "staff@personal.example",
            Password = "Password@123", SiteIds = [Guid.NewGuid()], Permissions = ["DEVICE_MANAGE"] };
        async Task Login(string role)
        {
            fixture.User.Accounts.Single().AccountRoles.Single().RoleCode = role;
            var r = await client.PostAsJsonAsync("/api/auth/login", new { email = "driver@gmail.com", password = "Password@123" });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var token = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString();
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/users", Body())).StatusCode);
        foreach (var role in new[] { "DRIVER", "SITE_OPERATOR", "PLATFORM_ADMIN" })
        {
            await Login(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/users", Body())).StatusCode);
        }
        Assert.Equal(0, operators.Calls);
        await Login("BUSINESS_OWNER");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        foreach (var body in new[] {
            new CreateOperatorDto(),
            BodyWith(b => b.FullName = "   "), BodyWith(b => b.Email = "invalid"),
            BodyWith(b => b.Password = "weakpass"), BodyWith(b => b.Password = "Password@12345678"),
            BodyWith(b => b.SiteIds = []), BodyWith(b => b.SiteIds = [Guid.Empty]),
            BodyWith(b => b.SiteIds = [b.SiteIds[0], b.SiteIds[0]]), BodyWith(b => b.SiteIds = null!),
            BodyWith(b => b.Permissions = []), BodyWith(b => b.Permissions = ["REFUND_APPROVE"]),
            BodyWith(b => b.Permissions = ["DEVICE_MANAGE", "DEVICE_MANAGE"]), BodyWith(b => b.Permissions = null!) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", body)).StatusCode);
        Assert.Equal(0, operators.Calls);
        var result = await client.PostAsJsonAsync("/api/users", Body());
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        Assert.Contains("no-store", result.Headers.CacheControl!.ToString());
        Assert.Equal(fixture.User.Id, operators.OwnerId);
        var json = await result.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        fixture.User.Status = SmartParking.UserService.Domain.Enum.UserStatus.Locked;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/users", Body())).StatusCode);
        Assert.Equal(1, operators.Calls);

        CreateOperatorDto BodyWith(Action<CreateOperatorDto> change) { var b = Body(); change(b); return b; }
    }

    private sealed class UnusedUsers : IUserManagementService
    {
        public Task<ManagedUsersPage> ListAsync(Guid adminId, UserSearchDto query, CancellationToken ct) => throw new NotSupportedException();
        public Task<ManagedUserDetail> GetAsync(Guid adminId, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<ManagedUserDto> SetStatusAsync(Guid adminId, Guid id, UserStatusDto body, CancellationToken ct) => throw new NotSupportedException();
    }
}
