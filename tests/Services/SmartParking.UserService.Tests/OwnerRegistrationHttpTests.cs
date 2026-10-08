using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UserService.API.Controllers;
using UserService.API.Middleware;
using UserService.Application;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class OwnerRegistrationHttpTests
{
    private sealed class Registrations : IOwnerRegistrationService
    {
        public int Calls;
        public Guid? Reviewer;
        private readonly OwnerApplicationDto record = new(Guid.NewGuid(), Guid.NewGuid(), "Owner", "Company",
            "owner@example.com", "0911111111", "basement", "pending", DateTimeOffset.UtcNow, null, null, null);
        public Task<OwnerApplicationDto> RegisterAsync(OwnerRegistrationDto body, CancellationToken ct)
        { Calls++; return Task.FromResult(record); }
        public Task<IReadOnlyList<OwnerApplicationDto>> ListAsync(Guid adminId, CancellationToken ct)
        { Calls++; return Task.FromResult<IReadOnlyList<OwnerApplicationDto>>([record]); }
        public Task<OwnerApplicationDto> ReviewAsync(Guid adminId, Guid id, ReviewOwnerDto body, CancellationToken ct)
        { Calls++; Reviewer = adminId; return Task.FromResult(record with { Status = body.Status }); }
    }

    [Fact]
    public async Task ValidationAuthorizationResponseAndRevokedRole()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddJWTAuthentication(builder.Configuration, true);
        builder.Services.AddAuthorization();
        var fixture = new AuthTestFixture();
        builder.Services.AddSingleton<IUnitOfWork>(fixture);
        builder.Services.AddSingleton<IAuthSessionStore>(fixture.Sessions);
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        var registrations = new Registrations();
        builder.Services.AddSingleton<IOwnerRegistrationService>(registrations);
        builder.Services.AddRateLimiter(options => options.AddPolicy("DriverOtp", _ =>
            System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test")));
        await using var app = builder.Build();
        app.UseMiddleware<AuthExceptionMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        OwnerRegistrationDto Body() => new() { FullName = "Owner", BusinessName = "Company", Email = "owner@example.com",
            Phone = "0911111111", Password = "Password@123", LotType = "basement", AgreedToPolicy = true };
        var invalid = Body(); invalid.AgreedToPolicy = false;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register/owner", invalid)).StatusCode);
        invalid = Body(); invalid.BusinessName = " ";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register/owner", invalid)).StatusCode);
        invalid = Body(); invalid.LotType = "indoor";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register/owner", invalid)).StatusCode);
        invalid = Body(); invalid.Password = "weak";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register/owner", invalid)).StatusCode);
        Assert.Equal(0, registrations.Calls);
        var created = await client.PostAsJsonAsync("/api/auth/register/owner", Body());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("pending", data.GetProperty("status").GetString());
        Assert.False(data.TryGetProperty("password", out _));
        Assert.False(data.TryGetProperty("accessToken", out _));
        var id = data.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/owner-applications")).StatusCode);
        async Task Login()
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "driver@gmail.com", password = "Password@123" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var session = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        }
        await Login();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/owner-applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync($"/api/owner-applications/{id}/review", new { status = "approved" })).StatusCode);
        fixture.User.Accounts.Single().AccountRoles.Single().RoleCode = "PLATFORM_ADMIN";
        await Login();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/owner-applications")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/owner-applications/{id}/review", new { status = "active" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/owner-applications/{id}/review", new { status = "approved", reviewNote = "OK" })).StatusCode);
        Assert.Equal(fixture.User.Id, registrations.Reviewer);
        fixture.User.Accounts.Single().AccountRoles.Single().RoleCode = "BUSINESS_OWNER";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/owner-applications")).StatusCode);
    }
}
