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

namespace SmartParking.NotificationAuditService.Tests.Adversarial;

public class AdversarialMilestone3ChallengeTests
{
    private const string ServerSecretKey = "SmartParkingAdversarialTestSecretKeyThatIsSecureAndAtLeast256BitsLong!";
    private const string UntrustedSecretKey = "CompletelyDifferentSecretKeyThatTheServerDoesNotTrustAtAll1234567890!";

    private class StubConnectedDbCreator : IDatabaseCreator
    {
        public bool CanConnect() => true;
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public bool EnsureCreated() => true;
        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public bool EnsureDeleted() => true;
        public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private class StubDisconnectedDbCreator : IDatabaseCreator
    {
        public bool CanConnect() => false;
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public bool EnsureCreated() => false;
        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public bool EnsureDeleted() => false;
        public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private class StubThrowingDbCreator : IDatabaseCreator
    {
        public bool CanConnect() => throw new TimeoutException("Database connection timeout during health probe.");
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => throw new TimeoutException("Database connection timeout during health probe.");
        public bool EnsureCreated() => false;
        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public bool EnsureDeleted() => false;
        public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private class AdversarialTestDbContext : NotificationAuditDbContext
    {
        public AdversarialTestDbContext(DbContextOptions<NotificationAuditDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }

    public enum DbProbeMode
    {
        Connected,
        Disconnected,
        Throwing
    }

    private class AdversarialApiFactory : WebApplicationFactory<Program>
    {
        private readonly string? _rsaPublicKeyPem;
        private readonly string _environment;
        private readonly DbProbeMode _dbMode;
        public readonly FakeNotificationRepository Repository = new();

        public AdversarialApiFactory(
            string? rsaPublicKeyPem = null,
            string environment = "Production",
            DbProbeMode dbMode = DbProbeMode.Connected)
        {
            _rsaPublicKeyPem = rsaPublicKeyPem;
            _environment = environment;
            _dbMode = dbMode;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);

            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                var dict = new Dictionary<string, string?>
                {
                    ["RabbitMQ:UseInMemory"] = "true",
                    ["Jwt:Issuer"] = "SmartParking.UserService",
                    ["Jwt:Audience"] = "SmartParking",
                    ["Jwt:SecretKey"] = ServerSecretKey
                };

                if (!string.IsNullOrWhiteSpace(_rsaPublicKeyPem))
                {
                    dict["Jwt:PublicKeyPem"] = _rsaPublicKeyPem;
                }

                config.AddInMemoryCollection(dict);
            });

            builder.ConfigureServices(services =>
            {
                var descriptors = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<NotificationAuditDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(NotificationAuditDbContext) ||
                    d.ServiceType == typeof(INotificationRepository)).ToList();

                foreach (var d in descriptors)
                {
                    services.Remove(d);
                }

                services.AddSingleton<INotificationRepository>(Repository);

                var optionsBuilder = new DbContextOptionsBuilder<NotificationAuditDbContext>()
                    .UseInMemoryDatabase("AdversarialTestDb_" + Guid.NewGuid());

                if (_dbMode == DbProbeMode.Disconnected)
                {
                    optionsBuilder.ReplaceService<IDatabaseCreator, StubDisconnectedDbCreator>();
                }
                else if (_dbMode == DbProbeMode.Throwing)
                {
                    optionsBuilder.ReplaceService<IDatabaseCreator, StubThrowingDbCreator>();
                }
                else
                {
                    optionsBuilder.ReplaceService<IDatabaseCreator, StubConnectedDbCreator>();
                }

                services.AddScoped<NotificationAuditDbContext>(sp => new AdversarialTestDbContext(optionsBuilder.Options));
            });
        }
    }

    private static string GenerateHmacToken(
        Guid userId,
        string role = "Driver",
        DateTime? expiresAt = null,
        string? signingKey = null,
        string issuer = "SmartParking.UserService",
        string audience = "SmartParking")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? ServerSecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("role", role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static (string Token, string PublicKeyPem, RSA RsaInstance) GenerateRsaToken(
        Guid userId,
        string role = "Driver",
        DateTime? expiresAt = null,
        RSA? customRsa = null,
        string issuer = "SmartParking.UserService",
        string audience = "SmartParking")
    {
        var rsa = customRsa ?? RSA.Create(2048);
        var key = new RsaSecurityKey(rsa);
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("role", role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
        return (tokenString, publicKeyPem, rsa);
    }

    // =========================================================================
    // SECTION 1: PROBE UNAUTHORIZED REQUESTS (All routes MUST return 401)
    // =========================================================================

    [Theory]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/notifications")]
    public async Task Probe1_Unauthorized_GetNotifications_Returns401(string route)
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/notifications/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/notifications/00000000-0000-0000-0000-000000000001")]
    public async Task Probe1_Unauthorized_GetNotificationById_Returns401(string route)
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/notifications/00000000-0000-0000-0000-000000000001/read")]
    [InlineData("/api/notifications/00000000-0000-0000-0000-000000000001/read")]
    public async Task Probe1_Unauthorized_PatchMarkAsRead_Returns401(string route)
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PatchAsync(route, content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/notifications/mark-all-read")]
    [InlineData("/api/notifications/mark-all-read")]
    public async Task Probe1_Unauthorized_PostMarkAllRead_Returns401(string route)
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync(route, content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/notifications/00000000-0000-0000-0000-000000000001/retry")]
    [InlineData("/api/notifications/00000000-0000-0000-0000-000000000001/retry")]
    public async Task Probe1_Unauthorized_PostRetryDelivery_Returns401(string route)
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync(route, content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // SECTION 2: PROBE VALID HMAC AND RSA TOKENS (Both MUST return 200 OK)
    // =========================================================================

    [Theory]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/notifications")]
    public async Task Probe2_ValidHmacToken_GetNotifications_Returns200Ok(string route)
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        var userId = Guid.NewGuid();
        var token = GenerateHmacToken(userId, "Driver");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", json);
    }

    [Theory]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/notifications")]
    public async Task Probe2_ValidRsaToken_GetNotifications_Returns200Ok(string route)
    {
        var userId = Guid.NewGuid();
        var (token, publicKeyPem, rsa) = GenerateRsaToken(userId, "Driver");
        using (rsa)
        {
            using var factory = new AdversarialApiFactory(rsaPublicKeyPem: publicKeyPem);
            var client = factory.CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync(route);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"success\":true", json);
        }
    }

    [Fact]
    public async Task Probe2_ValidHmacToken_MarkAllRead_Returns200OkWithCount()
    {
        using var factory = new AdversarialApiFactory();
        var userId = Guid.NewGuid();

        // Seed 2 notifications for this user
        var notif1 = DurableNotification.Create("TestEvent", "k1", userId, NotificationChannel.IN_APP, "TPL", "{}", title: "N1", message: "M1");
        var notif2 = DurableNotification.Create("TestEvent", "k2", userId, NotificationChannel.IN_APP, "TPL", "{}", title: "N2", message: "M2");
        await factory.Repository.AddAsync(notif1);
        await factory.Repository.AddAsync(notif2);

        var client = factory.CreateClient();
        var token = GenerateHmacToken(userId, "Driver");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/v1/notifications/mark-all-read", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"markedCount\":2", json);
    }

    [Fact]
    public async Task Probe2_ValidRsaToken_MarkAsRead_Returns200Ok()
    {
        var userId = Guid.NewGuid();
        var notif = DurableNotification.Create("TestEvent", "k3", userId, NotificationChannel.IN_APP, "TPL", "{}", title: "N3", message: "M3");

        var (token, publicKeyPem, rsa) = GenerateRsaToken(userId, "Driver");
        using (rsa)
        {
            using var factory = new AdversarialApiFactory(rsaPublicKeyPem: publicKeyPem);
            await factory.Repository.AddAsync(notif);

            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PatchAsync($"/api/v1/notifications/{notif.Id}/read", new StringContent("{}", Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"isRead\":true", json);
        }
    }

    [Theory]
    [InlineData("SmartParking.UserService.Demo", "SmartParking")]
    [InlineData("SmartParking.UserService", "SmartParking.Postman.Demo")]
    public async Task Probe2_AlternativeValidIssuersAndAudiences_Returns200Ok(string issuer, string audience)
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        var userId = Guid.NewGuid();
        var token = GenerateHmacToken(userId, "Driver", issuer: issuer, audience: audience);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // =========================================================================
    // SECTION 3: PROBE EXPIRED, TAMPERED, MALFORMED, UNTRUSTED TOKENS (MUST return 401)
    // =========================================================================

    [Fact]
    public async Task Probe3_ExpiredHmacToken_Returns401Unauthorized()
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        // Expired 10 minutes ago (well past the 1 minute clock skew)
        var expiredToken = GenerateHmacToken(Guid.NewGuid(), "Driver", expiresAt: DateTime.UtcNow.AddMinutes(-10));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Probe3_ExpiredRsaToken_Returns401Unauthorized()
    {
        var (token, publicKeyPem, rsa) = GenerateRsaToken(Guid.NewGuid(), "Driver", expiresAt: DateTime.UtcNow.AddMinutes(-10));
        using (rsa)
        {
            using var factory = new AdversarialApiFactory(rsaPublicKeyPem: publicKeyPem);
            var client = factory.CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/v1/notifications");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Probe3_TamperedHmacSignature_Returns401Unauthorized()
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        var validToken = GenerateHmacToken(Guid.NewGuid(), "Driver");
        var parts = validToken.Split('.');
        Assert.Equal(3, parts.Length);

        // Corrupt signature portion by replacing the last characters
        var tamperedSignature = parts[2].Substring(0, parts[2].Length - 4) + "XXXX";
        var tamperedToken = $"{parts[0]}.{parts[1]}.{tamperedSignature}";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Probe3_TamperedPayload_Returns401Unauthorized()
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        var validToken = GenerateHmacToken(Guid.NewGuid(), "Driver");
        var parts = validToken.Split('.');
        Assert.Equal(3, parts.Length);

        // Decode payload, modify role to Admin, re-encode without resigning
        var payloadBytes = Base64UrlEncoder.DecodeBytes(parts[1]);
        var payloadJson = Encoding.UTF8.GetString(payloadBytes);
        var tamperedPayloadJson = payloadJson.Replace("\"Driver\"", "\"SuperAdmin\"");
        var tamperedPayloadEncoded = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(tamperedPayloadJson));

        var tamperedToken = $"{parts[0]}.{tamperedPayloadEncoded}.{parts[2]}";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Probe3_UntrustedHmacSigningKey_Returns401Unauthorized()
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        // Sign with an untrusted secret key not known to the API
        var untrustedToken = GenerateHmacToken(Guid.NewGuid(), "Driver", signingKey: UntrustedSecretKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", untrustedToken);

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Probe3_UntrustedRsaKey_Returns401Unauthorized()
    {
        // Server knows Public Key A
        var (_, trustedPublicKeyPem, rsaA) = GenerateRsaToken(Guid.NewGuid(), "Driver");
        // Attacker creates Key B and signs with Key B
        using var rsaB = RSA.Create(2048);
        var (untrustedToken, _, _) = GenerateRsaToken(Guid.NewGuid(), "Driver", customRsa: rsaB);

        using (rsaA)
        {
            using var factory = new AdversarialApiFactory(rsaPublicKeyPem: trustedPublicKeyPem);
            var client = factory.CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", untrustedToken);

            var response = await client.GetAsync("/api/v1/notifications");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Probe3_UntrustedIssuer_Returns401Unauthorized()
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        var invalidIssuerToken = GenerateHmacToken(Guid.NewGuid(), "Driver", issuer: "https://evil-attacker.example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidIssuerToken);

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Probe3_UntrustedAudience_Returns401Unauthorized()
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        var invalidAudienceToken = GenerateHmacToken(Guid.NewGuid(), "Driver", audience: "UnintendedServiceTarget");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidAudienceToken);

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("not-a-valid-token")]
    [InlineData("Bearer abc.def")]
    [InlineData("Bearer")]
    [InlineData("Bearer ....")]
    [InlineData("Bearer null")]
    public async Task Probe3_MalformedTokenStrings_Returns401Unauthorized(string malformedAuthValue)
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", malformedAuthValue);

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Probe3_AlgorithmNoneAttack_Returns401Unauthorized()
    {
        using var factory = new AdversarialApiFactory();
        var client = factory.CreateClient();

        // Craft unsigned "alg":"none" token
        var header = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
        var payload = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes($"{{\"sub\":\"{Guid.NewGuid()}\",\"role\":\"Driver\",\"iss\":\"SmartParking.UserService\",\"aud\":\"SmartParking\"}}"));
        var noneToken = $"{header}.{payload}.";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", noneToken);

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // SECTION 4: PROBE HEADER SPOOFING IN NON-DEVELOPMENT ENVIRONMENTS
    // =========================================================================

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task Probe4_NonDev_HeaderSpoofingWithoutJwt_MustReturn401(string environment)
    {
        using var factory = new AdversarialApiFactory(environment: environment);
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-User-Role", "Admin");

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Probe4_NonDev_HeaderSpoofingWithExpiredJwt_MustReturn401(string environment)
    {
        using var factory = new AdversarialApiFactory(environment: environment);
        var client = factory.CreateClient();

        var expiredToken = GenerateHmacToken(Guid.NewGuid(), "Driver", expiresAt: DateTime.UtcNow.AddMinutes(-10));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-User-Role", "Admin");

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Probe4_Production_SpoofedHeadersAreIgnoredInFavorOfJwtClaims()
    {
        using var factory = new AdversarialApiFactory(environment: "Production");

        var legitimateDriverId = Guid.NewGuid();
        var spoofedVictimId = Guid.NewGuid();

        // Seed 1 notification for the legitimate driver, 0 for victim
        var notif = DurableNotification.Create(
            originatingEvent: "PaymentSucceeded",
            idempotencyKey: "idem-" + Guid.NewGuid(),
            recipientId: legitimateDriverId,
            channel: NotificationChannel.IN_APP,
            templateCode: "PAYMENT_RECEIPT",
            payload: "{}",
            title: "Driver Payment Receipt",
            message: "Your payment succeeded.");
        await factory.Repository.AddAsync(notif);

        var client = factory.CreateClient();

        // Legitimate driver token
        var token = GenerateHmacToken(legitimateDriverId, "Driver");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Attacker attempts to spoof victim ID in headers to view victim's notifications
        client.DefaultRequestHeaders.Add("X-User-Id", spoofedVictimId.ToString());
        client.DefaultRequestHeaders.Add("X-User-Role", "Admin");

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        // Response MUST contain legitimate driver's notification and NOT empty
        using var doc = JsonDocument.Parse(body);
        var dataArray = doc.RootElement.GetProperty("data");
        Assert.Equal(1, dataArray.GetArrayLength());
        Assert.Equal(legitimateDriverId.ToString(), dataArray[0].GetProperty("recipientId").GetString());
    }

    // =========================================================================
    // SECTION 5: PROBE TRUTHFUL HEALTHCHECK BEHAVIOR
    // =========================================================================

    [Fact]
    public async Task Probe5_HealthCheck_Connected_Returns200OkWithConnectedStatus()
    {
        using var factory = new AdversarialApiFactory(dbMode: DbProbeMode.Connected);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("Connected", doc.RootElement.GetProperty("dependencies").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Probe5_HealthCheck_Disconnected_Returns503WithDisconnectedStatus()
    {
        using var factory = new AdversarialApiFactory(dbMode: DbProbeMode.Disconnected);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Unhealthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("Disconnected", doc.RootElement.GetProperty("dependencies").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Probe5_HealthCheck_ThrowingException_Returns503WithDisconnectedStatus()
    {
        using var factory = new AdversarialApiFactory(dbMode: DbProbeMode.Throwing);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Unhealthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("Disconnected", doc.RootElement.GetProperty("dependencies").GetProperty("database").GetString());
    }
}
