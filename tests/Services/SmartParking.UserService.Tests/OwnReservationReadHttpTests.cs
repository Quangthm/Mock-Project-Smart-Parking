using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace SmartParking.UserService.Tests;

public sealed class OwnReservationReadHttpTests
{
    [Fact]
    public async Task RealHostEnforcesJwtAccountOwnershipAndFreshDetail()
    {
        await using var f = await Fixture.Create();
        var account = Guid.NewGuid(); var user = Guid.NewGuid(); var other = Guid.NewGuid();
        f.Store.Rows.Add((account, Row(f.Clock.Now.AddHours(-1), f.Clock.Now.AddHours(1))));
        f.Store.Rows.Add((other, Row(f.Clock.Now, f.Clock.Now.AddHours(1))));
        var list = await Data(await f.Get("/api/me/reservations", f.Token(account, user)));
        var id = Assert.Single(list.EnumerateArray()).GetProperty("reservationId").GetGuid();
        var detailPath = "/api/me/reservations/" + id;
        Assert.Equal(HttpStatusCode.OK, (await f.Get(detailPath, f.Token(account, user))).StatusCode);
        foreach (var actor in new[] { (other, user), (other, Guid.NewGuid()) })
            Assert.Equal(HttpStatusCode.NotFound, (await f.Get(detailPath, f.Token(actor.Item1, actor.Item2))).StatusCode);
        foreach (var role in new[] { "admin", "owner", "operator" })
            Assert.Equal(HttpStatusCode.Forbidden, (await f.Get(detailPath, f.Token(account, user, role))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Get(detailPath, "")).StatusCode);
        var expired = await f.Get(detailPath, f.Token(account, user, expired: true));
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode); Assert.Contains(expired.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Get(detailPath, f.Token(account, user, audience: "wrong"))).StatusCode);
        using var forged = new HttpRequestMessage(HttpMethod.Get, detailPath);
        forged.Headers.Authorization = new("Bearer", f.Token(other, user)); forged.Headers.Add("X-Account-Id", account.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.SendAsync(forged)).StatusCode);
        f.Store.Rows.RemoveAll(r => r.Item2.Id == id);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Get(detailPath, f.Token(account, user))).StatusCode);
    }

    [Fact]
    public async Task PagingOverlapValidationEmptyAndOutageAreDistinct()
    {
        await using var f = await Fixture.Create(); var account = Guid.NewGuid(); var user = Guid.NewGuid();
        var from = f.Clock.Now; var to = from.AddDays(1);
        f.Store.Rows.AddRange(new[] {
            (account, Row(from.AddHours(-2), from.AddHours(1))),
            (account, Row(from.AddHours(2), from.AddHours(3))),
            (account, Row(from.AddHours(-1), from)), // touching start: excluded
            (account, Row(to, to.AddHours(1))) }); // touching end: excluded
        var query = "?pageSize=1&from=" + Uri.EscapeDataString(from.ToString("O")) + "&to=" + Uri.EscapeDataString(to.ToString("O"));
        var page = await Data(await f.Get("/api/me/reservations" + query, f.Token(account, user)));
        var first = Assert.Single(page.EnumerateArray());
        Assert.Equal("CAR", first.GetProperty("vehicle").GetProperty("vehicleType").GetString());
        var continuation = "&before=" + Uri.EscapeDataString(first.GetProperty("createdAt").GetString()!) + "&beforeId=" + first.GetProperty("reservationId").GetGuid();
        var second = await Data(await f.Get("/api/me/reservations" + query + continuation, f.Token(account, user)));
        Assert.NotEqual(first.GetProperty("reservationId").GetGuid(), Assert.Single(second.EnumerateArray()).GetProperty("reservationId").GetGuid());
        Assert.Empty((await Data(await f.Get("/api/me/reservations" + query + continuation, f.Token(Guid.NewGuid(), user)))).EnumerateArray());
        foreach (var invalid in new[] { "?pageSize=0", "?pageSize=101", "?before=invalid", "?beforeId=" + Guid.NewGuid(), "?from=2030-01-01T00:00:00Z", "?from=2030-01-02T00:00:00Z&to=2030-01-01T00:00:00Z" })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Get("/api/me/reservations" + invalid, f.Token(account, user))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Get("/api/me/reservations/not-a-guid", f.Token(account, user))).StatusCode);
        Assert.Empty((await Data(await f.Get("/api/me/reservations?accountId=" + account, f.Token(Guid.NewGuid(), user)))).EnumerateArray());
        f.Store.Fail = true;
        var failure = await f.Get("/api/me/reservations", f.Token(account, user));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
        Assert.False((await failure.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task DisabledAdmissionRemainsFailClosed()
    {
        await using var f = await Fixture.Create(false);
        using var response = await f.Get("/api/me/reservations", f.Token(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("RESERVATION_ADMISSION_DISABLED", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    private static ReservationRow Row(DateTimeOffset start, DateTimeOffset end) => new() { Id = Guid.NewGuid(), SiteId = Guid.NewGuid(), VehicleType = "CAR", OriginalPlate = "SYNTHETIC", ExpectedStartTime = start, ExpectedEndTime = end, Status = "CONFIRMED", CreatedAt = start };
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").Clone();
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Store : IReservationBookingRepository
    {
        public List<(Guid, ReservationRow)> Rows = []; public bool Fail;
        public Task<ReservationRow?> FindOwnBookingAsync(Guid account, Guid id, CancellationToken ct) => Task.FromResult(Rows.Where(r => r.Item1 == account && r.Item2.Id == id).Select(r => r.Item2).SingleOrDefault());
        public Task<IReadOnlyList<ReservationRow>> ListOwnBookingsAsync(Guid account, int take, DateTimeOffset? before, Guid? beforeId, CancellationToken ct, DateTimeOffset? from = null, DateTimeOffset? to = null)
        {
            if (Fail) throw new ReservationGateException(503, "DEPENDENCY_UNAVAILABLE", "Synthetic outage");
            IReadOnlyList<ReservationRow> rows = Rows.Where(r => r.Item1 == account).Select(r => r.Item2)
                .Where(r => from is null || r.ExpectedStartTime < to && r.ExpectedEndTime > from)
                .Where(r => before is null || r.CreatedAt < before || r.CreatedAt == before && r.Id.CompareTo(beforeId!.Value) < 0)
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(take).ToArray();
            return Task.FromResult(rows);
        }
        public Task<DateTimeOffset> DatabaseNowAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task RequireDurableConfirmationAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task RequirePlateAvailableAsync(ReservationRow row, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReservationRow?> FindForUpdateAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReservationGateEntry>> ListAsync(Guid tenant, Guid site, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReservationGateEntry?> FindAsync(Guid tenant, Guid site, string query, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReservationGateEntry?> FindByIdAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task AddAsync(ReservationRow row, CancellationToken ct) => throw new NotSupportedException();
        public void Remove(ReservationRow row) => throw new NotSupportedException();
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public Clock Clock = new(); public Store Store = new(); public HttpClient Client = null!;
        private WebApplication app = null!; private readonly RSA key = RSA.Create(2048);
        public static async Task<Fixture> Create(bool enabled = true)
        {
            var f = new Fixture();
            f.app = OperationHost.Build(["--environment", "Testing"], b => {
                b.Configuration.Sources.Clear(); b.WebHost.UseTestServer();
                b.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                    ["Services:Key"] = new('x', 32), ["ConnectionStrings:Reservation"] = "Host=synthetic.invalid;Database=synthetic",
                    ["ReservationAdmission:Enabled"] = enabled.ToString(), ["ReservationAdmission:BrokerPassword"] = "synthetic-only",
                    ["JwtValidation:Issuer"] = "synthetic-user", ["JwtValidation:Audience"] = "synthetic-business",
                    ["JwtValidation:JwksUri"] = "https://synthetic.invalid/jwks"
                });
                // Replace external I/O only; exercise real routes, JWT middleware, actor, validators and handlers.
                b.Host.ConfigureContainer<IServiceCollection>((_, services) => {
                    foreach (var hosted in services.Where(x => x.ServiceType == typeof(IHostedService) &&
                        x.ImplementationType?.FullName != "Microsoft.AspNetCore.Hosting.GenericWebHostService").ToArray()) services.Remove(hosted);
                    services.RemoveAll<IReservationBookingRepository>();
                    services.AddSingleton<IReservationBookingRepository>(f.Store);
                    if (enabled) services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => {
                        var config = new OpenIdConnectConfiguration { Issuer = "synthetic-user" };
                        config.SigningKeys.Add(new RsaSecurityKey(f.key.ExportParameters(false)));
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(config);
                    });
                });

            });
            await f.app.StartAsync(); f.Client = f.app.GetTestClient(); return f;
        }
        public string Token(Guid account, Guid user, string role = "driver", bool expired = false, string audience = "synthetic-business")
        {
            var header = Base64UrlEncoder.Encode("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
            var payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new { iss = "synthetic-user", aud = audience, sub = user, account_id = account, jti = Guid.NewGuid().ToString("N"), role, nbf = Clock.Now.AddMinutes(-10).ToUnixTimeSeconds(), exp = Clock.Now.AddMinutes(expired ? -5 : 15).ToUnixTimeSeconds() }));
            var body = header + "." + payload; return body + "." + Base64UrlEncoder.Encode(key.SignData(Encoding.ASCII.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
        public Task<HttpResponseMessage> Get(string path, string token)
        { var request = new HttpRequestMessage(HttpMethod.Get, path); if (token != "") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); return Client.SendAsync(request); }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); key.Dispose(); }
    }
}
