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
using SmartParking.OperationService.Application;

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
        var id = Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
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
        var query = "?limit=1&from=" + Uri.EscapeDataString(from.ToString("O")) + "&to=" + Uri.EscapeDataString(to.ToString("O"));
        var page = await Data(await f.Get("/api/me/reservations" + query, f.Token(account, user)));
        Assert.Single(page.GetProperty("items").EnumerateArray());
        var cursor = Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!);
        var second = await Data(await f.Get("/api/me/reservations" + query + "&cursor=" + cursor, f.Token(account, user)));
        Assert.Single(second.GetProperty("items").EnumerateArray()); Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Get("/api/me/reservations" + query + "&cursor=" + cursor, f.Token(Guid.NewGuid(), user))).StatusCode);
        foreach (var invalid in new[] { "?limit=0", "?limit=51", "?cursor=forged", "?from=2030-01-01", "?from=2030-01-01T00:00:00Z", "?accountId=" + account, "?limit=1&limit=2" })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Get("/api/me/reservations" + invalid, f.Token(account, user))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Get("/api/me/reservations/not-a-guid", f.Token(account, user))).StatusCode);
        Assert.Empty((await Data(await f.Get("/api/me/reservations", f.Token(Guid.NewGuid(), user)))).GetProperty("items").EnumerateArray());
        f.Store.Fail = true;
        var failure = await f.Get("/api/me/reservations", f.Token(account, user));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
        Assert.False((await failure.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());
    }

    private static OwnReservation Row(DateTimeOffset start, DateTimeOffset end) => new(Guid.NewGuid(), Guid.NewGuid(), new("CAR", "SYNTHETIC"), start, end, "CONFIRMED", start);
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").Clone();
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.Parse("2032-02-28T17:00:00Z"); public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Store : IOwnReservationStore
    {
        public List<(Guid, OwnReservation)> Rows = []; public bool Fail;
        public Task<OwnReservation?> Get(Guid account, Guid id, DateTimeOffset asOf, CancellationToken ct) => Task.FromResult(Rows.Where(r => r.Item1 == account && r.Item2.Id == id).Select(r => r.Item2 with { AsOf = asOf }).SingleOrDefault());
        public Task<IReadOnlyList<OwnReservation>> List(Guid account, DateTimeOffset? from, DateTimeOffset? to, int limit, ReservationCursor? after, DateTimeOffset asOf, CancellationToken ct)
        {
            if (Fail) throw new OwnReadFailure(503, "DEPENDENCY_UNAVAILABLE");
            IReadOnlyList<OwnReservation> rows = Rows.Where(r => r.Item1 == account).Select(r => r.Item2)
                .Where(r => from is null || r.StartAt < to && r.EndAt > from)
                .Where(r => after is null || r.StartAt > after.Start || r.StartAt == after.Start && r.Id.CompareTo(after.Id) > 0)
                .OrderBy(r => r.StartAt).ThenBy(r => r.Id).Take(limit).Select(r => r with { AsOf = asOf }).ToArray();
            return Task.FromResult(rows);
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public Clock Clock = new(); public Store Store = new(); public HttpClient Client = null!;
        private WebApplication app = null!; private readonly RSA key = RSA.Create(2048);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            f.app = OperationHost.Build(["--environment", "Production"], b => {
                b.Configuration.Sources.Clear(); b.WebHost.UseTestServer();
                b.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Services:Key"] = new('x', 32), ["ConnectionStrings:Reservation"] = "Host=synthetic.invalid;Database=synthetic" });
                b.Services.AddSingleton<TimeProvider>(f.Clock); b.Services.AddSingleton<IOwnReservationStore>(f.Store);
                b.Services.AddSingleton(new OwnReadJwt("synthetic-user", "synthetic-business", f.key.ExportSubjectPublicKeyInfoPem()));
                b.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
            await f.app.StartAsync(); f.Client = f.app.GetTestClient(); return f;
        }
        public string Token(Guid account, Guid user, string role = "driver", bool expired = false, string audience = "synthetic-business")
        {
            var header = Base64UrlEncoder.Encode("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
            var payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new { iss = "synthetic-user", aud = audience, sub = user, account_id = account, jti = Guid.NewGuid().ToString("N"), role, nbf = Clock.Now.AddSeconds(-1).ToUnixTimeSeconds(), exp = Clock.Now.AddMinutes(expired ? 0 : 15).ToUnixTimeSeconds() }));
            var body = header + "." + payload; return body + "." + Base64UrlEncoder.Encode(key.SignData(Encoding.ASCII.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
        public Task<HttpResponseMessage> Get(string path, string token)
        { var request = new HttpRequestMessage(HttpMethod.Get, path); if (token != "") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); return Client.SendAsync(request); }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); key.Dispose(); }
    }
}
