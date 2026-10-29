using UserService.API;
using Microsoft.Extensions.Configuration;
using UserService.Application.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UserService.API.Controllers;
using UserService.API.Extensions;
using UserService.API.Middleware;
using UserService.Application;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;
using UserService.Infrastructure.Services;
using SmartParking.UserService.Domain.Entities;

namespace SmartParking.UserService.Tests;

public sealed class OperatorProvisioningHttpTests
{
    private sealed class OperatorRepository : UnusedIdentityRepository
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public User? CreatedOperator { get; private set; }
        public Guid? CreatedBy { get; private set; }

        public override Task<Guid[]> OwnerTenantsAsync(Guid actor, string permission, CancellationToken cancellationToken)
            => Task.FromResult(new[] { TenantId });

        public override Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public override void AddOperator(User user, IEnumerable<OperatorGrant> grants, AuthChallenge challenge, WorkflowDelivery delivery)
        {
            CreatedOperator = user;
            CreatedBy = grants.Single().CreatedBy;
        }
        public Task<IReadOnlyList<OperatorDto>> ListAsync(Guid ownerId,CancellationToken ct) => Task.FromResult<IReadOnlyList<OperatorDto>>([]);
        public Task<IReadOnlyList<OperatorAssignment>> AssignmentsAsync(Guid operatorId,CancellationToken ct) => Task.FromResult<IReadOnlyList<OperatorAssignment>>([]);
    }

    private sealed class RecordedAccessEvents : UnusedAccessEvents
    {
        public List<OperatorAccessChanged> Messages { get; } = [];
        public override void Stage(OperatorAccessChanged message) => Messages.Add(message);
    }

    [Fact]
    public async Task OwnerOnlyValidationAndSecretFreeResponse()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(UsersController).Assembly);
        builder.Services.AddApiErrorResponses();
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiCurrentActor();
        builder.Services.AddApplicationServices(builder.Configuration.GetSection("AuthenticationPolicy").Get<global::UserService.Application.Common.Models.JwT.AuthenticationPolicy>() ?? new());
        builder.Services.AddSingleton<IVehicleRepository, UnsupportedVehicleRepository>();
        var repository = new OperatorRepository();
        var events = new RecordedAccessEvents();
        var directory = new TestParkingDirectory();
        var siteId = Guid.NewGuid();
        directory.Sites[siteId] = repository.TenantId;
        builder.Services.AddSingleton<IIdentityAccessRepository>(repository);
        builder.Services.AddSingleton<IAccessEventStore>(events);
        builder.Services.AddSingleton<IParkingDirectory>(directory);
        builder.Services.AddSingleton<IWorkflowProtector, TestWorkflowProtector>();
        builder.Services.AddJWTAuthentication(builder.Configuration, true); builder.Services.AddAuthorization();
        var fixture = new AuthTestFixture(); var operators = new Operators();
        TestAuthorization.AddServices(builder.Services, fixture);
        builder.Services.AddSingleton<IUnitOfWork>(fixture);
        builder.Services.AddSingleton<ICurrentAccessStore>(fixture);
        builder.Services.AddSingleton<IEventPublisher, RecordingProfilePublisher>();
        builder.Services.AddSingleton<IAuthSessionStore>(fixture.Sessions);
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        // Use the real command handler; only persistence and external ports are substitutes.
        builder.Services.AddSingleton<IUserManagementService, UnusedUsers>();
        await using var app = builder.Build();
        app.UseRouting();
        app.UseApiErrorResponses(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync(); using var client = app.GetTestClient();
        CreateOperatorDto Body() => new() { FullName = "Operator", Email = "staff@personal.example",
            SiteIds = [siteId], Permissions = ["DEVICE_MANAGE"] };
        async Task Login(string role)
        {
            fixture.User.Accounts.Single().RoleCode = role;
            fixture.User.Accounts.Single().TenantId = role == "BUSINESS_OWNER" ? Guid.NewGuid() : null;
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
        Assert.Null(repository.CreatedOperator);
        await Login("BUSINESS_OWNER");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        foreach (var body in new[] {
            new CreateOperatorDto(),
            BodyWith(b => b.FullName = "   "), BodyWith(b => b.Email = "invalid"),
            BodyWith(b => b.SiteIds = []), BodyWith(b => b.SiteIds = [Guid.Empty]),
            BodyWith(b => b.SiteIds = [b.SiteIds[0], b.SiteIds[0]]), BodyWith(b => b.SiteIds = null!),
            BodyWith(b => b.Permissions = []), BodyWith(b => b.Permissions = ["REFUND_APPROVE"]),
            BodyWith(b => b.Permissions = ["DEVICE_MANAGE", "DEVICE_MANAGE"]), BodyWith(b => b.Permissions = null!) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", body)).StatusCode);
        var obsoleteRequest = new { fullName = "Operator", email = "staff@personal.example",
            password = "Password@123", siteIds = new[] { siteId }, permissions = new[] { "DEVICE_MANAGE" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", obsoleteRequest)).StatusCode);
        Assert.Null(repository.CreatedOperator);
        var result = await client.PostAsJsonAsync("/api/users", Body());
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        Assert.Contains("no-store", result.Headers.CacheControl!.ToString());
        Assert.Equal(fixture.User.Id, repository.CreatedBy);
        Assert.NotNull(repository.CreatedOperator);
        Assert.Empty(repository.CreatedOperator.PasswordHash);
        Assert.Equal(SmartParking.UserService.Domain.Enum.UserStatus.PendingVerification, repository.CreatedOperator.Status);
        Assert.Equal("pending_verification", Assert.Single(events.Messages).Status);
        var json = await result.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        fixture.User.Status = SmartParking.UserService.Domain.Enum.UserStatus.Locked;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/users", Body())).StatusCode);
        Assert.Single(events.Messages);

        CreateOperatorDto BodyWith(Action<CreateOperatorDto> change) { var b = Body(); change(b); return b; }
    }

    private sealed class UnusedUsers : IUserManagementService
    {
        public Task<ManagedUsersPage> ListAsync(Guid adminId, UserSearchDto query, CancellationToken ct) => throw new NotSupportedException();
        public Task<ManagedUserDetail> GetAsync(Guid adminId, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<ManagedUserDto> SetStatusAsync(Guid adminId, Guid id, UserStatusDto body, CancellationToken ct) => throw new NotSupportedException();
    }
}
