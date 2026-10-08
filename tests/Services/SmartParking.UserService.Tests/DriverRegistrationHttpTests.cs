using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UserService.API.Controllers;
using UserService.API.Middleware;
using UserService.Application;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Infrastructure.Services;
using UserService.Application.DTOs;

namespace SmartParking.UserService.Tests;

public sealed class DriverRegistrationHttpTests
{
    private sealed class Registrations : IDriverRegistrationService
    {
        public int Calls;
        public Task<DriverRegistrationResult> RegisterAsync(DriverRegistrationDto body, CancellationToken ct)
        { Calls++; return Task.FromResult(new DriverRegistrationResult(Guid.NewGuid(), "email", DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(1))); }
        public Task VerifyAsync(VerifyDriverOtpDto body, CancellationToken ct) { Calls++; return Task.CompletedTask; }
        public Task<DriverRegistrationResult> ResendAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(new DriverRegistrationResult(id, "email", DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(1)));
        public Task<DriverRegistrationResult> RecoverAsync(string contact,CancellationToken ct)=>ResendAsync(Guid.NewGuid(),ct);
    }

    [Fact]
    public async Task HttpValidationContractsAndRateLimit()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddApplicationServices(builder.Configuration);
        var fixture = new AuthTestFixture();
        builder.Services.AddJWTAuthentication(builder.Configuration, true);
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IUnitOfWork>(fixture);
        builder.Services.AddSingleton<IAuthSessionStore>(fixture.Sessions);
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        var registrations = new Registrations();
        builder.Services.AddSingleton<IDriverRegistrationService>(registrations);
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("DriverOtp", _ => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter("test", _ => new()
            { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        await using var app = builder.Build();
        app.UseMiddleware<AuthExceptionMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        foreach (var body in new[] {
            new { fullName = "Driver", email = "bad-email", phone = "", password = "Password@123" },
            new { fullName = "Driver", email = "driver@example.com", phone = "letters", password = "Password@123" },
            new { fullName = "Driver", email = "driver@example.com", phone = "", password = "weak" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register/driver", body)).StatusCode);
        Assert.Equal(0, registrations.Calls);
        var created = await client.PostAsJsonAsync("/api/auth/register/driver", new { fullName = "Driver", email = "driver@example.com", password = "Password@123" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var id = data.GetProperty("registrationId").GetGuid();
        Assert.False(data.TryGetProperty("code", out _));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register/driver/verify", new { registrationId = id, code = "123" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register/driver/verify", new { registrationId = id, code = "123456" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register/driver/resend", new { registrationId = id })).StatusCode);
        for (var i = 0; i < 3; i++) await client.PostAsJsonAsync("/api/auth/register/driver/resend", new { registrationId = id });
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/register/driver/resend", new { registrationId = id })).StatusCode);
    }
}
