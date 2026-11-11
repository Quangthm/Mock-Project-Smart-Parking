using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Persistence.Context;
using SmartParking.NotificationAuditService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.NotificationAuditService.Tests.Integration;

/// <summary>
/// Empirical Adversarial Challenge Test Suite for Milestone 3 (R4):
/// Security, Controller Endpoints & Truthful Healthcheck.
/// </summary>
public class NotificationMilestone3AdversarialTests
{
    private const string HmacKey = "Milestone3AdversarialTestSecretKeyVeryLongAndSecure1234567890!";

    #region Test Doubles & Custom Factory

    private class StubConnectedDatabaseCreator : IDatabaseCreator
    {
        public bool CanConnect() => true;
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public bool EnsureCreated() => true;
        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public bool EnsureDeleted() => true;
        public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private class StubDisconnectedDatabaseCreator : IDatabaseCreator
    {
        public bool CanConnect() => false;
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public bool EnsureCreated() => false;
        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public bool EnsureDeleted() => false;
        public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private class StubThrowingDatabaseCreator : IDatabaseCreator
    {
        public bool CanConnect() => throw new TimeoutException("Database connection timeout simulation.");
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => throw new TimeoutException("Database connection timeout simulation.");
        public bool EnsureCreated() => false;
        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public bool EnsureDeleted() => false;
        public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private class TestAuditDbContext : NotificationAuditDbContext
    {
        public TestAuditDbContext(DbContextOptions<NotificationAuditDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }

    private enum DbSimulationMode
    {
        Connected,
        Disconnected,
        ThrowsException
    }

    private class Milestone3TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly FakeNotificationRepository _sharedRepository;
        private readonly DbSimulationMode _dbMode;
        private readonly string? _rsaPublicKeyPem;

        public Milestone3TestApiFactory(
            FakeNotificationRepository sharedRepository,
            DbSimulationMode dbMode = DbSimulationMode.Connected,
            string? rsaPublicKeyPem = null)
        {
            _sharedRepository = sharedRepository;
            _dbMode = dbMode;
            _rsaPublicKeyPem = rsaPublicKeyPem;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production"); // Always enforce strict production mode

            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["RabbitMQ:UseInMemory"] = "true",
                    ["Jwt:Issuer"] = "SmartParking.UserService",
                    ["Jwt:Audience"] = "SmartParking",
                    ["Jwt:SecretKey"] = HmacKey
                };

                if (!string.IsNullOrWhiteSpace(_rsaPublicKeyPem))
                {
                    settings["Jwt:PublicKeyPem"] = _rsaPublicKeyPem;
                }

                config.AddInMemoryCollection(settings);
            });

            builder.ConfigureServices(services =>
            {
                // Remove existing DB and repository descriptors
                var descriptors = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<NotificationAuditDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(NotificationAuditDbContext) ||
                    d.ServiceType == typeof(INotificationRepository) ||
                    d.ServiceType == typeof(IUnitOfWork)).ToList();

                foreach (var d in descriptors)
                {
                    services.Remove(d);
                }

                // Register singleton shared repository so test setup reflects directly into controller
                services.AddSingleton<INotificationRepository>(_sharedRepository);
                services.AddScoped<IUnitOfWork, FakeUnitOfWork>();

                var optionsBuilder = new DbContextOptionsBuilder<NotificationAuditDbContext>()
                    .UseInMemoryDatabase("AdversarialTestDb_" + Guid.NewGuid());

                switch (_dbMode)
                {
                    case DbSimulationMode.Connected:
                        optionsBuilder.ReplaceService<IDatabaseCreator, StubConnectedDatabaseCreator>();
                        break;
                    case DbSimulationMode.Disconnected:
                        optionsBuilder.ReplaceService<IDatabaseCreator, StubDisconnectedDatabaseCreator>();
                        break;
                    case DbSimulationMode.ThrowsException:
                        optionsBuilder.ReplaceService<IDatabaseCreator, StubThrowingDatabaseCreator>();
                        break;
                }

                services.AddScoped<NotificationAuditDbContext>(sp => new TestAuditDbContext(optionsBuilder.Options));
            });
        }
    }

    #endregion

    #region Helper Methods

    private static string GenerateValidJwt(Guid userId, string role = "driver")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(HmacKey));
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

    private static DurableNotification CreateSampleNotification(Guid recipientId, string title = "Test Notification")
    {
        return DurableNotification.Create(
            originatingEvent: "PaymentSucceededV1",
            idempotencyKey: Guid.NewGuid().ToString(),
            recipientId: recipientId,
            channel: NotificationChannel.IN_APP,
            templateCode: "PAYMENT_CONFIRMATION",
            payload: "{}",
            maxAttempts: 3,
            scheduledAt: DateTimeOffset.UtcNow,
            title: title,
            message: "Payment processed successfully.");
    }

    #endregion

    #region 1. Probe PATCH /api/v1/notifications/{id}/read & Dual Routes (404, 403, 200)

    [Theory]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/notifications")]
    public async Task PatchRead_NonExistentNotificationId_Returns404NotFound(string routePrefix)
    {
        // Arrange
        var repo = new FakeNotificationRepository();
        using var factory = new Milestone3TestApiFactory(repo);
        var client = factory.CreateClient();

        var callerUserId = Guid.NewGuid();
        var token = GenerateValidJwt(callerUserId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var nonExistentId = Guid.NewGuid();
        var requestUrl = $"{routePrefix}/{nonExistentId}/read";

        // Act
        var response = await client.PatchAsync(requestUrl, new StringContent("{}", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Contains(nonExistentId.ToString(), root.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/notifications")]
    public async Task PatchRead_CrossUserMismatch_Returns403ForbiddenAndDoesNotMarkAsRead(string routePrefix)
    {
        // Arrange
        var repo = new FakeNotificationRepository();
        var ownerUserId = Guid.NewGuid();
        var attackerUserId = Guid.NewGuid();

        var ownerNotification = CreateSampleNotification(ownerUserId, "Owner's Confidential Alert");
        repo.Notifications.Add(ownerNotification);

        using var factory = new Milestone3TestApiFactory(repo);
        var client = factory.CreateClient();

        // Attacker attempts to mark owner's notification
        var attackerToken = GenerateValidJwt(attackerUserId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", attackerToken);

        var requestUrl = $"{routePrefix}/{ownerNotification.Id}/read";

        // Act
        var response = await client.PatchAsync(requestUrl, new StringContent("{}", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        // Assert HTTP 403 Forbidden with standard error structure
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("FORBIDDEN", root.GetProperty("errorCode").GetString());
        Assert.Contains("permission", root.GetProperty("message").GetString());

        // Assert empirical invariant: notification state is UNCHANGED in persistence
        var targetInRepo = repo.Notifications.First(n => n.Id == ownerNotification.Id);
        Assert.False(targetInRepo.IsRead);
        Assert.Null(targetInRepo.ReadAt);
    }

    [Theory]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/notifications")]
    public async Task PatchRead_OwnerAuthorized_Returns200OkAndMarksAsRead(string routePrefix)
    {
        // Arrange
        var repo = new FakeNotificationRepository();
        var ownerUserId = Guid.NewGuid();

        var ownerNotification = CreateSampleNotification(ownerUserId, "Owner's Legitimate Alert");
        repo.Notifications.Add(ownerNotification);

        using var factory = new Milestone3TestApiFactory(repo);
        var client = factory.CreateClient();

        var ownerToken = GenerateValidJwt(ownerUserId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var requestUrl = $"{routePrefix}/{ownerNotification.Id}/read";

        // Act
        var response = await client.PatchAsync(requestUrl, new StringContent("{}", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        // Assert HTTP 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());

        var data = root.GetProperty("data");
        Assert.True(data.GetProperty("isRead").GetBoolean());
        Assert.NotNull(data.GetProperty("readAt").GetString());

        // Verify persistence state
        var targetInRepo = repo.Notifications.First(n => n.Id == ownerNotification.Id);
        Assert.True(targetInRepo.IsRead);
        Assert.NotNull(targetInRepo.ReadAt);
    }

    #endregion

    #region 2. Probe POST mark-all-read on Dual Routes & Partition Isolation

    [Theory]
    [InlineData("/api/v1/notifications/mark-all-read")]
    [InlineData("/api/notifications/mark-all-read")]
    public async Task PostMarkAllRead_DualRoutes_MarksOnlyCallerNotificationsAndPreservesOthers(string endpoint)
    {
        // Arrange
        var repo = new FakeNotificationRepository();
        var callerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        // Seed 3 unread for caller
        var callerNotif1 = CreateSampleNotification(callerUserId, "Caller Notif 1");
        var callerNotif2 = CreateSampleNotification(callerUserId, "Caller Notif 2");
        var callerNotif3 = CreateSampleNotification(callerUserId, "Caller Notif 3");
        repo.Notifications.AddRange(new[] { callerNotif1, callerNotif2, callerNotif3 });

        // Seed 2 unread for other user
        var otherNotif1 = CreateSampleNotification(otherUserId, "Other Notif 1");
        var otherNotif2 = CreateSampleNotification(otherUserId, "Other Notif 2");
        repo.Notifications.AddRange(new[] { otherNotif1, otherNotif2 });

        using var factory = new Milestone3TestApiFactory(repo);
        var client = factory.CreateClient();

        var callerToken = GenerateValidJwt(callerUserId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", callerToken);

        // Act
        var response = await client.PostAsync(endpoint, new StringContent("{}", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        // Assert HTTP 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());

        // Marked count should be exactly 3
        Assert.Equal(3, root.GetProperty("markedCount").GetInt32());
        Assert.Equal(3, root.GetProperty("data").GetProperty("markedCount").GetInt32());

        // Assert caller notifications are now all read
        var callerNotifications = repo.Notifications.Where(n => n.RecipientId == callerUserId).ToList();
        Assert.Equal(3, callerNotifications.Count);
        Assert.All(callerNotifications, n => Assert.True(n.IsRead));

        // Assert other user's notifications remain UNTOUCHED (isRead == false)
        var otherNotifications = repo.Notifications.Where(n => n.RecipientId == otherUserId).ToList();
        Assert.Equal(2, otherNotifications.Count);
        Assert.All(otherNotifications, n => Assert.False(n.IsRead));
    }

    [Fact]
    public async Task PostMarkAllRead_WhenNoUnreadNotifications_Returns200WithZeroCount()
    {
        // Arrange
        var repo = new FakeNotificationRepository();
        var callerUserId = Guid.NewGuid();

        using var factory = new Milestone3TestApiFactory(repo);
        var client = factory.CreateClient();

        var callerToken = GenerateValidJwt(callerUserId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", callerToken);

        // Act
        var response = await client.PostAsync("/api/v1/notifications/mark-all-read", new StringContent("{}", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(0, root.GetProperty("markedCount").GetInt32());
    }

    #endregion

    #region 3. Probe Truthful /health Endpoint DB Connectivity Simulation

    [Fact]
    public async Task HealthEndpoint_WhenDatabaseIsConnected_Returns200OkWithConnectedStatus()
    {
        // Arrange
        var repo = new FakeNotificationRepository();
        using var factory = new Milestone3TestApiFactory(repo, DbSimulationMode.Connected);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        // Assert HTTP 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.Equal("Connected", root.GetProperty("dependencies").GetProperty("database").GetString());
    }

    [Fact]
    public async Task HealthEndpoint_WhenDatabaseIsDisconnected_Returns503ServiceUnavailableWithDisconnectedStatus()
    {
        // Arrange
        var repo = new FakeNotificationRepository();
        using var factory = new Milestone3TestApiFactory(repo, DbSimulationMode.Disconnected);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        // Assert HTTP 503 Service Unavailable
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());
        Assert.Equal("Disconnected", root.GetProperty("dependencies").GetProperty("database").GetString());
    }

    [Fact]
    public async Task HealthEndpoint_WhenDatabaseThrowsException_Returns503ServiceUnavailableWithDisconnectedStatus()
    {
        // Arrange
        var repo = new FakeNotificationRepository();
        using var factory = new Milestone3TestApiFactory(repo, DbSimulationMode.ThrowsException);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        // Assert HTTP 503 Service Unavailable (graceful catch without crashing service)
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());
        Assert.Equal("Disconnected", root.GetProperty("dependencies").GetProperty("database").GetString());
    }

    #endregion

    #region 4. Adversarial Security Boundary Stress Tests

    [Fact]
    public async Task Security_ExpiredJwtToken_Returns401Unauthorized()
    {
        // Arrange
        var repo = new FakeNotificationRepository();
        using var factory = new Milestone3TestApiFactory(repo);
        var client = factory.CreateClient();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(HmacKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiredToken = new JwtSecurityToken(
            issuer: "SmartParking.UserService",
            audience: "SmartParking",
            claims: new[] { new Claim("sub", Guid.NewGuid().ToString()) },
            expires: DateTime.UtcNow.AddMinutes(-10), // expired 10 minutes ago
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(expiredToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);

        // Act
        var response = await client.GetAsync("/api/v1/notifications");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Security_InvalidSignatureJwtToken_Returns401Unauthorized()
    {
        // Arrange
        var repo = new FakeNotificationRepository();
        using var factory = new Milestone3TestApiFactory(repo);
        var client = factory.CreateClient();

        var bogusKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("AnotherKeyThatIsNotConfiguredInServiceAtAll1234567890!"));
        var creds = new SigningCredentials(bogusKey, SecurityAlgorithms.HmacSha256);
        var forgedToken = new JwtSecurityToken(
            issuer: "SmartParking.UserService",
            audience: "SmartParking",
            claims: new[] { new Claim("sub", Guid.NewGuid().ToString()) },
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(forgedToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);

        // Act
        var response = await client.GetAsync("/api/v1/notifications");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Security_ProductionHeaderSpoofing_RejectedWith401Unauthorized()
    {
        // Arrange
        var repo = new FakeNotificationRepository();
        using var factory = new Milestone3TestApiFactory(repo); // runs in Production
        var client = factory.CreateClient();

        // Spoof headers without Bearer token
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-User-Role", "admin");

        // Act
        var response = await client.GetAsync("/api/v1/notifications");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
