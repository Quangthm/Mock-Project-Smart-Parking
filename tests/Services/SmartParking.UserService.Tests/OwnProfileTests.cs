using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using SmartParking.UserService.Domain.ValueObjects;
using UserService.API.Controllers;
using UserService.API.Extensions;
using UserService.Application;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Profiles.Commands;
using UserService.Application.Profiles.Events;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class OwnProfileTests
{
    [Fact]
    public void NameCountsUnicodeScalarsAndPreservesInteriorSpacing()
    {
        Assert.True(ProfileName.IsValid(string.Concat(Enumerable.Repeat("\U0001F600", 255))));
        Assert.False(ProfileName.IsValid(string.Concat(Enumerable.Repeat("\U0001F600", 256))));
        Assert.True(ProfileName.IsValid(new string('a', 253) + "e\u0301"));
        Assert.False(ProfileName.IsValid(new string('a', 254) + "e\u0301"));
        foreach (var invalid in new[] { null, "", "   ", "bad\nname", "\tName", "bad\0name", "bad\ud800" })
            Assert.False(ProfileName.IsValid(invalid));
        var user = new User { FullName = "Before" };
        user.UpdateFullName("  Nguyễn  An  ");
        Assert.Equal("Nguyễn  An", user.FullName);
        Assert.Throws<ArgumentException>(() => user.UpdateFullName("\r"));
        Assert.Equal("Nguyễn  An", user.FullName);
    }

    [Fact]
    public async Task NarrowMutationAttributesEventAndNoOpDoesNotPublishAgain()
    {
        var f = new AuthTestFixture();
        var actor = await ActorAsync(f);
        var events = new RecordingProfilePublisher();
        f.User.AvatarUrl = "https://legacy.example.test/" + new string('a', 300);
        f.User.Phone = "+84912345678";
        f.User.Accounts.Single().Permissions = [];
        var password = f.User.PasswordHash;
        var handler = new UpdateOwnProfileCommandHandler(f, f, actor, events, f.Clock);
        var response = await handler.Handle(new("  Nguyễn  An  "), default);
        Assert.Equal("Nguyễn  An", response.FullName);
        Assert.Equal(f.User.AvatarUrl, response.AvatarUrl);
        Assert.Equal("driver@gmail.com", f.User.Email);
        Assert.Equal("+84912345678", f.User.Phone);
        Assert.Equal(password, f.User.PasswordHash);
        Assert.Equal(UserStatus.Active, f.User.Status);
        Assert.Empty(f.User.Accounts.Single().Permissions!);
        Assert.Equal("ACTIVE", f.User.Accounts.Single().Status);
        var fact = Assert.IsType<UserProfileNameChanged>(Assert.Single(events.Events));
        Assert.Equal(actor.AccountId, fact.ActorAccountId);
        Assert.Equal(f.User.Id, fact.AffectedUserId);
        Assert.Equal(1, fact.SchemaVersion);
        await handler.Handle(new(" Nguyễn  An "), default);
        Assert.Single(events.Events);
        Assert.Equal(1, f.Saved);
    }

    [Fact]
    public async Task ExactAccountAndSessionCannotBeRescuedByAnotherAccount()
    {
        var f = new AuthTestFixture();
        var actor = await ActorAsync(f);
        var events = new RecordingProfilePublisher();
        f.User.Accounts.Add(new Account { Id = Guid.NewGuid(), UserId = f.User.Id!.Value,
            Status = "ACTIVE", AccountRoles = [new AccountRole { RoleCode = "DRIVER" }] });
        f.User.Accounts.First().Status = "LOCKED";
        var handler = new UpdateOwnProfileCommandHandler(f, f, actor, events, f.Clock);
        Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() => handler.Handle(new("After"), default))).Code);
        f.User.Accounts.First().Status = "ACTIVE";
        var session = f.Sessions.Snapshot.Single();
        await f.Sessions.RevokeAsync(session.AccessTokenId, session.UserId, session.TokenHash, default);
        Assert.Equal("INVALID_TOKEN", (await Assert.ThrowsAsync<AuthException>(() => handler.Handle(new("After"), default))).Code);
        Assert.Equal("Demo Driver", f.User.FullName);
        Assert.Empty(events.Events);
        Assert.Equal(0, f.Saved);
    }

    [Theory]
    [InlineData("DRIVER", "driver")]
    [InlineData("SITE_OPERATOR", "operator")]
    [InlineData("BUSINESS_OWNER", "owner")]
    [InlineData("PLATFORM_ADMIN", "admin")]
    public async Task AllRolesReadAndUpdateThroughRealMediator(string roleCode, string wireRole)
    {
        await using var host = await ProfileHost.StartAsync(roleCode);
        await host.AuthenticateAsync();
        host.Fixture.User.AvatarUrl = "https://legacy.example.test/a.png";
        var read = await host.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Contains("no-store", read.Headers.CacheControl!.ToString());
        var data = (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(wireRole, data.GetProperty("role").GetString());
        Assert.Equal(host.Fixture.User.AvatarUrl, data.GetProperty("avatarUrl").GetString());
        var update = await host.Client.PatchAsJsonAsync("/api/auth/me", new { fullName = " Ada  Lovelace " });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        data = (await update.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("Ada  Lovelace", data.GetProperty("fullName").GetString());
        Assert.Equal(new[] { "avatarUrl", "email", "fullName", "role", "userId" }, data.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Single(host.Events.Events);
    }

    [Fact]
    public async Task UnknownFieldsCannotMassAssignAndAvatarMutationHasNoRoute()
    {
        await using var host = await ProfileHost.StartAsync();
        await host.AuthenticateAsync();
        var f = host.Fixture;
        f.User.AvatarUrl = "  legacy:existing value  ";
        var response = await host.Client.PatchAsJsonAsync("/api/auth/me", new {
            fullName = "After", userId = Guid.NewGuid(), accountId = Guid.NewGuid(), role = "admin",
            status = "LOCKED", email = "other@example.test", phone = "changed", password = "changed",
            avatarUrl = "https://arbitrary.example.test", avatar = new { reference = "invented" }, permissions = new[] { "ALL" }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("After", f.User.FullName);
        Assert.Equal("  legacy:existing value  ", f.User.AvatarUrl);
        Assert.Equal("driver@gmail.com", f.User.Email);
        Assert.Null(f.User.Phone);
        Assert.Equal(UserStatus.Active, f.User.Status);
        Assert.Equal("DRIVER", f.User.Accounts.Single().AccountRoles.Single().RoleCode);
        Assert.Null(f.User.Accounts.Single().Permissions);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.DeleteAsync("/api/auth/me/avatar")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/profile/me")).StatusCode);
    }

    [Theory]
    [InlineData("{\"fullName\":", false)]
    [InlineData("{\"fullName\":123}", false)]
    [InlineData("{\"fullName\":\"   \"}", true)]
    public async Task BindingAndValidatorFailuresNeverPublish(string json, bool fieldValidation)
    {
        await using var host = await ProfileHost.StartAsync();
        await host.AuthenticateAsync();
        var response = await host.Client.PatchAsync("/api/auth/me", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        if (fieldValidation) Assert.Equal("FullName", body.GetProperty("errors")[0].GetProperty("field").GetString());
        Assert.Equal("Demo Driver", host.Fixture.User.FullName);
        Assert.Empty(host.Events.Events);
    }

    [Fact]
    public async Task MissingInvalidRevokedAndLockedCredentialsCannotReadOrUpdate()
    {
        await using var host = await ProfileHost.StartAsync();
        async Task Denied()
        {
            foreach (var response in new[] { await host.Client.GetAsync("/api/auth/me"),
                await host.Client.PatchAsJsonAsync("/api/auth/me", new { fullName = "After" }) })
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
            }
        }
        await Denied();
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        await Denied();
        await host.AuthenticateAsync();
        var session = host.Fixture.Sessions.Snapshot.Single();
        await host.Fixture.Sessions.RevokeAsync(session.AccessTokenId, session.UserId, session.TokenHash, default);
        await Denied();
        await host.AuthenticateAsync();
        host.Fixture.User.Accounts.Single().Status = "LOCKED";
        host.Fixture.User.Accounts.Add(new Account { Id = Guid.NewGuid(), UserId = host.Fixture.User.Id!.Value,
            Status = "ACTIVE", AccountRoles = [new AccountRole { RoleCode = "DRIVER" }] });
        await Denied();
        Assert.Empty(host.Events.Events);
    }

    internal static async Task<TestCurrentActor> ActorAsync(AuthTestFixture fixture)
    {
        await fixture.Auth.CreateAsync(fixture.User, default);
        var session = fixture.Sessions.Snapshot.Last();
        return new(fixture.User.Id, "driver", session.AccountId, session.AccessTokenId);
    }

    private sealed class ProfileHost(WebApplication app, AuthTestFixture fixture, RecordingProfilePublisher events) : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();
        public AuthTestFixture Fixture => fixture;
        public RecordingProfilePublisher Events => events;
        public static async Task<ProfileHost> StartAsync(string role = "DRIVER")
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers().AddApplicationPart(typeof(ProfileController).Assembly);
            builder.Services.AddApiErrorResponses();
            builder.Services.AddApplicationServices(builder.Configuration);
            builder.Services.AddApiCurrentActor();
            builder.Services.AddSingleton<IVehicleRepository, UnsupportedVehicleRepository>();
            builder.Services.AddJWTAuthentication(builder.Configuration, true);
            builder.Services.AddAuthorization();
            var f = new AuthTestFixture();
            f.User.Accounts.Single().AccountRoles.Single().RoleCode = role;
            var publisher = new RecordingProfilePublisher();
            builder.Services.AddSingleton<IUnitOfWork>(f);
            builder.Services.AddSingleton<IProfileAccessStore>(f);
            builder.Services.AddSingleton<IAuthSessionStore>(f.Sessions);
            builder.Services.AddSingleton<IEventPublisher>(publisher);
            builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
            builder.Services.AddSingleton<TimeProvider>(f.Clock);
            var app = builder.Build();
            app.UseRouting(); app.UseApiErrorResponses();
            app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            return new(app, f, publisher);
        }
        public async Task AuthenticateAsync()
        {
            var response = await Client.PostAsJsonAsync("/api/auth/login", new { email = "driver@gmail.com", password = "Password@123" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            Client.DefaultRequestHeaders.Authorization = new("Bearer", data.GetProperty("accessToken").GetString());
        }
        public async ValueTask DisposeAsync() => await app.DisposeAsync();
    }
}

internal sealed class RecordingProfilePublisher : IEventPublisher
{
    public List<IIntegrationEventMessage> Events { get; } = [];
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class, IIntegrationEventMessage
    {
        cancellationToken.ThrowIfCancellationRequested(); Events.Add(message); return Task.CompletedTask;
    }
    public async Task PublishManyAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken) where T : class, IIntegrationEventMessage
    {
        foreach (var message in messages) await PublishAsync(message, cancellationToken);
    }
}
