using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Persistence.Context;
using SmartParking.NotificationAuditService.Tests.TestDoubles;

namespace SmartParking.NotificationAuditService.Tests.Integration;

public class ApiSecurityAndRoutingIntegrationTests
{
    private const string TestHmacSecretKey = "SmartParkingIntegrationTestSecretKeyThatIsVeryLongAndSecure1234567890!";

    private class TestConnectedDatabaseCreator : IDatabaseCreator
    {
        public bool CanConnect() => true;
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public bool EnsureCreated() => true;
        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public bool EnsureDeleted() => true;
        public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private class TestDisconnectedDatabaseCreator : IDatabaseCreator
    {
        public bool CanConnect() => false;
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public bool EnsureCreated() => false;
        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public bool EnsureDeleted() => false;
        public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private class TestIntegrationDbContext : NotificationAuditDbContext
    {
        public TestIntegrationDbContext(DbContextOptions<NotificationAuditDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Non-relational in-memory test db bypasses Npgsql relational mappings
        }
    }

    private class CustomApiFactory : WebApplicationFactory<Program>
    {
        private readonly string? _rsaPublicKeyPem;
        private readonly bool _simulateDbDown;

        public CustomApiFactory(string? rsaPublicKeyPem = null, bool simulateDbDown = false)
        {
            _rsaPublicKeyPem = rsaPublicKeyPem;
            _simulateDbDown = simulateDbDown;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production"); // Test in Production to enforce real security rules

            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                var inMemorySettings = new Dictionary<string, string?>
                {
                    ["RabbitMQ:UseInMemory"] = "true",
                    ["Jwt:Issuer"] = "SmartParking.UserService",
                    ["Jwt:Audience"] = "SmartParking",
                    ["Jwt:SecretKey"] = TestHmacSecretKey
                };

                if (!string.IsNullOrWhiteSpace(_rsaPublicKeyPem))
                {
                    inMemorySettings["Jwt:PublicKeyPem"] = _rsaPublicKeyPem;
                }

                config.AddInMemoryCollection(inMemorySettings);
            });

            builder.ConfigureServices(services =>
            {
                // Remove all existing DbContext and DbContextOptions registrations
                var descriptors = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<NotificationAuditDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(NotificationAuditDbContext) ||
                    d.ServiceType == typeof(INotificationRepository)).ToList();

                foreach (var d in descriptors)
                {
                    services.Remove(d);
                }

                services.AddScoped<INotificationRepository, FakeNotificationRepository>();

                var optionsBuilder = new DbContextOptionsBuilder<NotificationAuditDbContext>()
                    .UseInMemoryDatabase("IntegrationTestDb_" + Guid.NewGuid());

                if (_simulateDbDown)
                {
                    optionsBuilder.ReplaceService<IDatabaseCreator, TestDisconnectedDatabaseCreator>();
                }
                else
                {
                    optionsBuilder.ReplaceService<IDatabaseCreator, TestConnectedDatabaseCreator>();
                }

                services.AddScoped<NotificationAuditDbContext>(sp => new TestIntegrationDbContext(optionsBuilder.Options));
            });
        }
    }

    private static string GenerateHmacJwt(Guid userId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestHmacSecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("role", role)
        };
        var token = new JwtSecurityToken(
            issuer: "SmartParking.UserService",
            audience: "SmartParking",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static (string Token, string PublicKeyPem) GenerateRsaJwt(Guid userId, string role)
    {
        var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa);
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("role", role)
        };
        var token = new JwtSecurityToken(
            issuer: "SmartParking.UserService",
            audience: "SmartParking",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
        return (tokenString, publicKeyPem);
    }

    [Theory]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/notifications")]
    public async Task GetNotifications_Unauthenticated_ShouldReturn401Unauthorized(string route)
    {
        using var factory = new CustomApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/notifications/00000000-0000-0000-0000-000000000001/read")]
    [InlineData("/api/notifications/00000000-0000-0000-0000-000000000001/read")]
    public async Task PatchRead_Unauthenticated_ShouldReturn401Unauthorized(string route)
    {
        using var factory = new CustomApiFactory();
        var client = factory.CreateClient();

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PatchAsync(route, content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/notifications/mark-all-read")]
    [InlineData("/api/notifications/mark-all-read")]
    public async Task PostMarkAllRead_Unauthenticated_ShouldReturn401Unauthorized(string route)
    {
        using var factory = new CustomApiFactory();
        var client = factory.CreateClient();

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync(route, content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NonDevelopment_HeaderSpoofingWithoutJwt_ShouldReturn401Unauthorized()
    {
        using var factory = new CustomApiFactory();
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-User-Role", "admin");

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/notifications")]
    public async Task GetNotifications_WithValidHmacToken_ShouldAuthenticateAndReachController(string route)
    {
        using var factory = new CustomApiFactory();
        var client = factory.CreateClient();

        var token = GenerateHmacJwt(Guid.NewGuid(), "driver");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(route);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}. Body: {body}");
        Assert.Contains("\"success\":true", body);
    }

    [Theory]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/notifications")]
    public async Task GetNotifications_WithValidRsaToken_ShouldAuthenticateAndReachController(string route)
    {
        var (token, publicKeyPem) = GenerateRsaJwt(Guid.NewGuid(), "driver");

        using var factory = new CustomApiFactory(rsaPublicKeyPem: publicKeyPem);
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(route);

        // Authenticated successfully; returns 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", body);
    }

    [Fact]
    public async Task HealthCheck_WhenDatabaseIsHealthy_ShouldReturn200OkWithConnectedStatus()
    {
        using var factory = new CustomApiFactory(simulateDbDown: false);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}. Body: {body}");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.Equal("Connected", root.GetProperty("dependencies").GetProperty("database").GetString());
    }

    [Fact]
    public async Task HealthCheck_WhenDatabaseIsDown_ShouldReturn503ServiceUnavailableWithDisconnectedStatus()
    {
        using var factory = new CustomApiFactory(simulateDbDown: true);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());
        Assert.Equal("Disconnected", root.GetProperty("dependencies").GetProperty("database").GetString());
    }

    [Fact]
    public async Task AuditSearch_WithoutToken_ShouldReturn401Unauthorized()
    {
        using var factory = new CustomApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/audit-projections");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Driver")]
    [InlineData("Owner")]
    [InlineData("Operator")]
    public async Task AuditSearch_WithNonAdminRole_ShouldReturn403Forbidden(string role)
    {
        using var factory = new CustomApiFactory();
        var client = factory.CreateClient();
        var token = GenerateHmacJwt(Guid.NewGuid(), role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/audit-projections");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuditSearch_WithAdminRole_ShouldReturnPaginatedRedactedResultsAndRejectMutations()
    {
        using var factory = new CustomApiFactory();
        var client = factory.CreateClient();

        var seededId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();
            var projection = SmartParking.NotificationAuditService.Domain.Entities.CentralAuditProjection.Create(
                originatingService: "UserService",
                eventId: Guid.NewGuid(),
                actorId: actorId,
                actorRole: "Admin",
                action: "ADMIN_REVIEWED_APPLICATION",
                resourceType: "ParkingLot",
                resourceId: lotId,
                details: "{\"lotId\":\"" + lotId + "\",\"accessToken\":\"raw-secret-jwt\",\"note\":\"Approved\"}",
                occurredAt: DateTimeOffset.UtcNow.AddSeconds(-15));
            typeof(SmartParking.NotificationAuditService.Domain.Entities.CentralAuditProjection)
                .GetProperty("Id")!
                .SetValue(projection, seededId);
            db.CentralAuditProjections.Add(projection);
            await db.SaveChangesAsync();
        }

        var token = GenerateHmacJwt(actorId, "Admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var searchResponse = await client.GetAsync($"/api/v1/audit-projections?action=ADMIN_REVIEWED&lotId={lotId}&page=1&pageSize=10");
        var searchBody = await searchResponse.Content.ReadAsStringAsync();
        Assert.True(searchResponse.StatusCode == HttpStatusCode.OK, $"Expected OK but got {searchResponse.StatusCode}. Body: {searchBody}");
        Assert.Contains("[REDACTED]", searchBody);
        Assert.DoesNotContain("raw-secret-jwt", searchBody);
        Assert.Contains("\"isAsynchronousProjection\":true", searchBody);

        var detailResponse = await client.GetAsync($"/api/v1/audit-projections/{seededId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        var notFoundResponse = await client.GetAsync($"/api/v1/audit-projections/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/api/v1/audit-projections/{seededId}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task AuditSearch_WithInvalidQueryParameters_ShouldReturn400BadRequest()
    {
        using var factory = new CustomApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateHmacJwt(Guid.NewGuid(), "Admin"));

        var badPageResponse = await client.GetAsync("/api/v1/audit-projections?page=0&pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, badPageResponse.StatusCode);

        var badRangeResponse = await client.GetAsync(
            "/api/v1/audit-projections?from=2026-11-26T10:00:00Z&to=2026-11-26T09:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, badRangeResponse.StatusCode);
    }

    [Fact]
    public async Task AuditSearch_AfterDuplicateEventIngestion_ShouldReturnSingleDeduplicatedProjection()
    {
        using var factory = new CustomApiFactory();
        var client = factory.CreateClient();

        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            var command = new SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.RecordAuditProjectionCommand
            {
                OriginatingService = "OperationService",
                EventId = eventId,
                ActorId = actorId,
                ActorRole = "Operator",
                Action = "REPLAY_DEDUPE_TEST",
                ResourceType = "ParkingLot",
                ResourceId = lotId,
                Details = "{\"lotId\":\"" + lotId + "\",\"otp\":\"999999\"}",
                OccurredAt = DateTimeOffset.UtcNow.AddSeconds(-5)
            };

            var first = await mediator.Send(command);
            var second = await mediator.Send(command);
            Assert.False(first.IsDuplicate);
            Assert.True(second.IsDuplicate);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateHmacJwt(Guid.NewGuid(), "Admin"));

        var response = await client.GetAsync($"/api/v1/audit-projections?action=REPLAY_DEDUPE_TEST&lotId={lotId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(1, data.GetProperty("totalCount").GetInt32());
        var item = data.GetProperty("items")[0];
        Assert.Equal(eventId, item.GetProperty("eventId").GetGuid());
        Assert.Contains("[REDACTED]", item.GetProperty("details").GetString());
        Assert.DoesNotContain("999999", item.GetProperty("details").GetString());
    }
}
