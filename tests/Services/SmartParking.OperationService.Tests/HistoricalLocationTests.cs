using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Discovery;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Infrastructure.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests;

public sealed class HistoricalLocationTests : ICurrentActor, IHistoricalBookingSiteReader, IHistoricalSiteLocationOwner
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private Guid account = Guid.NewGuid();
    private HistoricalBookingSite? booking;
    private FacilitySiteLocation? location;
    private int ownerReads;

    public Guid? UserId { get; set; } = Guid.NewGuid();
    public Guid? AccountId => account;
    public string? Role { get; set; } = "driver";

    public Task<HistoricalBookingSite?> LatestConfirmedAsync(Guid accountId, CancellationToken ct)
    {
        Assert.Equal(account, accountId);
        return Task.FromResult(booking);
    }

    public Task<FacilitySiteLocation?> ReadPublishedAsync(Guid siteId, CancellationToken ct)
    {
        ownerReads++;
        Assert.Equal(booking!.SiteId, siteId);
        return Task.FromResult(location);
    }

    [QuoteDatabaseFact]
    public async Task DatabaseSelectsLatestConfirmationWithIdTieAndAccountIsolation()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("SMARTPARK_QUOTE_TEST_DB"))
            .Options;
        await using var db = new ReservationDbContext(options);
        var foreign = Guid.NewGuid();
        var earlySite = Guid.NewGuid();
        var latestSite = Guid.NewGuid();
        var tiedSite = Guid.NewGuid();
        var rows = new[]
        {
            Row(account, earlySite, "CONFIRMED", Now.AddHours(-2), Now.AddHours(10), 1),
            Row(account, latestSite, "ALLOCATED", Now, Now.AddHours(-10), 2),
            Row(account, tiedSite, "PARKING", Now, Now.AddHours(-11), 3),
            Row(account, Guid.NewGuid(), "COMPLETED", Now.AddHours(-3), Now.AddHours(11), 4),
            Row(account, Guid.NewGuid(), "CONFIRMED", null, Now.AddHours(12), 5),
            Row(account, Guid.NewGuid(), "PENDING_PAYMENT", Now.AddHours(2), Now, 6),
            Row(account, Guid.NewGuid(), "EXPIRED", Now.AddHours(2), Now, 7),
            Row(account, Guid.NewGuid(), "CANCELLED", Now.AddHours(2), Now, 8),
            Row(account, Guid.NewGuid(), "UNFULFILLABLE", Now.AddHours(2), Now, 9),
            Row(foreign, Guid.NewGuid(), "CONFIRMED", Now.AddHours(4), Now, 10)
        };
        db.Reservations.AddRange(rows);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var reader = new HistoricalBookingSiteReader(db);
        Assert.Equal(new HistoricalBookingSite(tiedSite, Now), await reader.LatestConfirmedAsync(account, default));
        Assert.Equal(rows[^1].SiteId, (await reader.LatestConfirmedAsync(foreign, default))!.SiteId);
        Assert.Null(await reader.LatestConfirmedAsync(Guid.NewGuid(), default));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(rows.Length, await db.Reservations.CountAsync(r => rows.Select(x => x.Id).Contains(r.Id)));
    }

    [Fact]
    public async Task HandlerReturnsOnlyLatestUsableSiteAndSkipsOwnerForEmptyHistory()
    {
        var handler = new ReadHistoricalLocationHandler(this, this, this);
        Assert.Null(await handler.Handle(new(), default));
        Assert.Equal(0, ownerReads);
        var site = Guid.NewGuid();
        booking = new(site, Now);
        Assert.Null(await handler.Handle(new(), default));
        Assert.Equal(1, ownerReads);
        location = new(site, "Published site", 10.75m, 106.7m);
        Assert.Equal(new HistoricalLocation(site, "Published site", 10.75m, 106.7m, Now),
            await handler.Handle(new(), default));
        Assert.Equal(2, ownerReads);
        location = new(Guid.NewGuid(), "Wrong site", 10m, 100m);
        var error = await Assert.ThrowsAsync<ReservationGateException>(() => handler.Handle(new(), default));
        Assert.Equal("OWNER_CONTRACT_INVALID", error.Code);
        Role = "operator";
        error = await Assert.ThrowsAsync<ReservationGateException>(() => handler.Handle(new(), default));
        Assert.Equal(403, error.StatusCode);
        Assert.Equal(3, ownerReads);
    }

    [Fact]
    public async Task PublicRouteRejectsAnonymousAndWrongRoleBeforeDatabaseAccess()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("historical-location-test-signing-key-long-enough"));
        await using var host = OperationHost.Build(
            ["--environment", "Testing", "--Services:Key", "historical-location-service-key-long-enough",
             "--ConnectionStrings:Reservation", "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused",
             "--Messaging:Enabled", "false", "--ReservationIntegration:Enabled", "false"],
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true, IssuerSigningKey = key,
                        ValidateIssuer = true, ValidIssuer = "historical-test",
                        ValidateAudience = true, ValidAudience = "historical-test",
                        ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
                        RoleClaimType = "role"
                    });
            });
        await host.StartAsync();
        using var client = host.GetTestClient();
        const string route = "/api/me/discovery/last-booked-location";
        using var anonymous = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var jwt = new JwtSecurityToken("historical-test", "historical-test",
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("account_id", Guid.NewGuid().ToString()),
             new Claim("role", "operator")],
            expires: DateTime.UtcNow.AddMinutes(2),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        using var wrongRole = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
    }

    [Fact]
    public async Task FacilityClientDistinguishesAbsenceFromFailureAndMalformedResponse()
    {
        using var rsa = RSA.Create(2048);
        var siteId = Guid.NewGuid();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:AllowHttp"] = "true",
            ["ReservationIntegration:OperationsPrivateKeyPem"] = rsa.ExportRSAPrivateKeyPem()
        }).Build();
        var status = HttpStatusCode.OK;
        var body = "{\"success\":true,\"data\":null}";
        using var transport = new StubHttpMessageHandler(request =>
        {
            Assert.Equal($"/internal/discovery/v1/sites/{siteId:D}/location", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Null(request.Content);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        });
        using var http = new HttpClient(transport) { BaseAddress = new Uri("http://localhost:5045/") };
        var client = new ReservationFacilityClient(http, config);
        Assert.Null(await client.ReadPublishedAsync(siteId, default));
        body = $"{{\"success\":true,\"data\":{{\"siteId\":\"{siteId}\",\"siteName\":\"Published site\",\"latitude\":10.75,\"longitude\":106.7}}}}";
        Assert.Equal(new FacilitySiteLocation(siteId, "Published site", 10.75m, 106.7m),
            await client.ReadPublishedAsync(siteId, default));
        status = HttpStatusCode.ServiceUnavailable;
        Assert.Equal("OWNER_UNAVAILABLE", (await Assert.ThrowsAsync<ReservationGateException>(
            () => client.ReadPublishedAsync(siteId, default))).Code);
        status = HttpStatusCode.OK;
        body = "{\"success\":true,\"data\":{\"siteId\":\"wrong\"}}";
        Assert.Equal("OWNER_UNAVAILABLE", (await Assert.ThrowsAsync<ReservationGateException>(
            () => client.ReadPublishedAsync(siteId, default))).Code);
    }

    private static ReservationRow Row(Guid accountId, Guid siteId, string status,
        DateTimeOffset? confirmedAt, DateTimeOffset createdAt, int id) => new()
    {
        Id = new Guid($"00000000-0000-0000-0000-{id:000000000000}"), TenantId = Guid.NewGuid(),
        SiteId = siteId, AccountId = accountId, VehicleType = "CAR", Status = status,
        ConfirmedAt = confirmedAt, CreatedAt = createdAt,
        ExpectedStartTime = Now.AddDays(2), ExpectedEndTime = Now.AddDays(2).AddHours(1)
    };
}
