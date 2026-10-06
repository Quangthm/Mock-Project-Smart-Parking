using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using UserService.API.Controllers;
using UserService.API.Middleware;
using UserService.Application;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class AuthHttpTests
{
    private sealed class Api : IAsyncDisposable
    {
        public AuthTestFixture Fixture { get; } = new();
        public WebApplication App { get; private set; } = null!;
        public HttpClient Client { get; private set; } = null!;
        public static async Task<Api> Start()
        {
            var api = new Api();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "http-test", ["Jwt:Audience"] = "http-test"
            });
            builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
            builder.Services.AddApplicationServices(builder.Configuration);
            builder.Services.AddJWTAuthentication(builder.Configuration, true);
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton<IUnitOfWork>(api.Fixture);
            builder.Services.AddSingleton<IAuthSessionStore>(api.Fixture.Sessions);
            builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
            builder.Services.AddSingleton<TimeProvider>(api.Fixture.Clock);
            api.App = builder.Build();
            api.App.UseMiddleware<AuthExceptionMiddleware>();
            api.App.UseAuthentication();
            api.App.UseAuthorization();
            api.App.MapControllers();
            await api.App.StartAsync();
            api.Client = api.App.GetTestClient();
            return api;
        }
        public async Task<JsonElement> Login()
        {
            var response = await Client.PostAsJsonAsync("/api/auth/login", new { email = "driver@gmail.com", password = "Password@123" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            return payload.GetProperty("data");
        }
        public void Bearer(JsonElement session) => Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        public async ValueTask DisposeAsync() { Client.Dispose(); await App.DisposeAsync(); }
    }

    private static async Task Code(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task LoginContract_AndOnlyApprovedRoute()
    {
        await using var api = await Api.Start();
        var session = await api.Login();
        Assert.Equal(3600, session.GetProperty("expiresIn").GetInt32());
        Assert.Equal("Bearer", session.GetProperty("tokenType").GetString());
        Assert.False(string.IsNullOrEmpty(session.GetProperty("refreshToken").GetString()));
        Assert.Equal("driver", session.GetProperty("user").GetProperty("role").GetString());
        api.Bearer(session);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.PostAsJsonAsync("/api/v1/auth/login", new { email = "driver@gmail.com", password = "Password@123" })).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"email\":\"driver@gmail.com\"}")]
    [InlineData("{\"password\":\"Password@123\"}")]
    [InlineData("{\"email\":\"invalid\",\"password\":\"Password@123\"}")]
    public async Task LoginValidation_Returns400(string json)
    {
        await using var api = await Api.Start();
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.PostAsync("/api/auth/login", new StringContent(json, Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Fact]
    public async Task InvalidCredentialsAndLockoutHaveRequiredCodesAndStatuses()
    {
        await using var api = await Api.Start();
        for (var i = 0; i < 3; i++)
        {
            var response = await api.Client.PostAsJsonAsync("/api/auth/login", new { email = "driver@gmail.com", password = "wrong" });
            await Code(response, i == 2 ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, i == 2 ? "ACCOUNT_LOCKED" : "AUTH_FAILED");
        }
        await Code(await api.Client.PostAsJsonAsync("/api/auth/login", new { email = "driver@gmail.com", password = "Password@123" }), HttpStatusCode.Forbidden, "ACCOUNT_LOCKED");
        await Code(await api.Client.PostAsJsonAsync("/api/auth/login", new { email = "unknown@gmail.com", password = "Password@123" }), HttpStatusCode.Unauthorized, "AUTH_FAILED");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"refreshToken\":null}")]
    [InlineData("{\"refreshToken\":\" \"}")]
    public async Task LogoutMissingRefreshReturns400WithoutRevokingSession(string? json)
    {
        await using var api = await Api.Start();
        var session = await api.Login(); api.Bearer(session);
        await Code(await api.Client.PostAsync("/api/auth/logout", json is null ? null : new StringContent(json, Encoding.UTF8, "application/json")), HttpStatusCode.BadRequest, "MISSING_REFRESH_TOKEN");
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bad-token")]
    public async Task LogoutRequiresValidBearer(string? token)
    {
        await using var api = await Api.Start();
        if (token is not null) api.Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await api.Client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = "unknown" });
        await Code(response, HttpStatusCode.Unauthorized, "INVALID_TOKEN");
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Fact]
    public async Task Logout200AndBothTokensBecomeUnusable()
    {
        await using var api = await Api.Start();
        var session = await api.Login(); api.Bearer(session);
        var refresh = session.GetProperty("refreshToken").GetString();
        var response = await api.Client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());
        await Code(await api.Client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "INVALID_TOKEN");
        await Code(await api.Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh }), HttpStatusCode.Unauthorized, "INVALID_TOKEN");
    }

    [Fact]
    public async Task WrongRefreshTokenReturns401AndDoesNotRevokeSession()
    {
        await using var api = await Api.Start();
        var first = await api.Login(); var second = await api.Login(); api.Bearer(first);
        await Code(await api.Client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = second.GetProperty("refreshToken").GetString() }), HttpStatusCode.Unauthorized, "INVALID_TOKEN");
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/auth/me")).StatusCode);
        api.Bearer(second);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task FailedRevokeReturns500AndPreservesSession()
    {
        await using var api = await Api.Start();
        var session = await api.Login(); api.Bearer(session); api.Fixture.Sessions.FailRevoke = true;
        await Code(await api.Client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = session.GetProperty("refreshToken").GetString() }), HttpStatusCode.InternalServerError, "REVOKE_SESSION_FAILED");
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task RefreshRotatesPairAndRejectsReplay()
    {
        await using var api = await Api.Start();
        var session = await api.Login(); api.Bearer(session);
        var refresh = session.GetProperty("refreshToken").GetString();
        var response = await api.Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var next = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        await Code(await api.Client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "INVALID_TOKEN");
        api.Bearer(next);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/auth/me")).StatusCode);
        await Code(await api.Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh }), HttpStatusCode.Unauthorized, "INVALID_TOKEN");
    }

    [Theory]
    [InlineData("locked")]
    [InlineData("role")]
    [InlineData("expired")]
    public async Task CurrentAccountAndSessionStateOverrideJwtValidity(string change)
    {
        await using var api = await Api.Start();
        var session = await api.Login(); api.Bearer(session);
        if (change == "locked") api.Fixture.User.Status = SmartParking.UserService.Domain.Enum.UserStatus.Locked;
        if (change == "role") api.Fixture.User.Accounts.First().AccountRoles.First().RoleCode = "ADMIN";
        if (change == "expired") api.Fixture.Clock.Now = api.Fixture.Clock.Now.AddHours(1);
        await Code(await api.Client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "INVALID_TOKEN");
    }
}
