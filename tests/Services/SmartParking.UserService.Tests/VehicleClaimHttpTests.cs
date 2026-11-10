using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace SmartParking.UserService.Tests;

public sealed partial class VehicleRegistrationTests
{
    [PostgresFact, Trait("Category", "Postgres")]
    public async Task UploadedClaimIsPrivateAndForgedFieldsCannotChangeOwnership()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var a = Driver("claim-http-a@example.test"); var b = Driver("claim-http-b@example.test");
        var c = Driver("claim-http-c@example.test");
        await using (var db = fixture.Open()) { db.Users.AddRange(a,b,c); await db.SaveChangesAsync(); }
        await using var app = await fixture.StartHostAsync(configure: builder => MediaTestSupport.Configure(builder, new FakeMediaProvider()));
        using var client = app.GetTestClient();
        async Task Login(string email)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password@123" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString());
        }
        async Task<Guid> Upload()
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(MediaTestSupport.Image()), "file", "vehicle.png");
            form.Add(new StringContent(Guid.NewGuid().ToString()), "draftIntentId");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles/images") { Content = form };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("assetId").GetGuid();
        }
        async Task<JsonElement> Data(string route) => (await (await client.GetAsync(route)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        await Login(a.Email!);
        var aImage = await Upload();
        var created = await client.PostAsJsonAsync("/api/vehicles", new { plate = " 51a-123.45 ", vehicleType = "CAR", imageAssetId = aImage,
            accountId = b.Accounts.Single().Id, status = "PENDING_DISPUTE", canonicalPlate = "FORGED" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var incumbent = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
        Assert.Equal(" 51a-123.45 ", (await Data("/api/vehicles"))[0].GetProperty("rawPlate").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/vehicles", new { plate = "51A12345", vehicleType = "CAR", imageAssetId = aImage })).StatusCode);
        Assert.Equal(0, (await Data("/api/vehicles/registration-claims")).GetArrayLength());
        await Login(b.Email!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/media/images/{aImage}/content")).StatusCode);
        var bImage = await Upload();
        var submitted = await client.PostAsJsonAsync("/api/vehicles", new { plate = "51A12345", vehicleType = "CAR", imageAssetId = bImage });
        Assert.Equal(HttpStatusCode.Conflict, submitted.StatusCode);
        Assert.Equal(0, (await Data("/api/vehicles")).GetArrayLength());
        var claim = (await Data("/api/vehicles/registration-claims"))[0];
        Assert.Equal("PENDING_DISPUTE", claim.GetProperty("status").GetString());
        Assert.False(claim.TryGetProperty("accountId", out _));
        Assert.NotEqual(incumbent, claim.GetProperty("id").GetGuid());
        client.DefaultRequestHeaders.Add("X-Service-Key", "isolated-user-service-tests-key-20261012");
        var binding = await client.PostAsJsonAsync("/internal/vehicle-bindings/resolve", new { accountId = a.Accounts.Single().Id, vehicleId = incumbent });
        var active = (await binding.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("ACTIVE", active.GetProperty("state").GetString());
        Assert.Equal("CAR", active.GetProperty("vehicleType").GetString());
        var pendingBinding = await client.PostAsJsonAsync("/internal/vehicle-bindings/resolve", new { accountId = b.Accounts.Single().Id, vehicleId = claim.GetProperty("id").GetGuid() });
        Assert.Equal("INELIGIBLE", (await pendingBinding.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("state").GetString());
        client.DefaultRequestHeaders.Remove("X-Service-Key");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/media/images/{bImage}/content")).StatusCode);
        await Login(c.Email!);
        Assert.Equal(0, (await Data("/api/vehicles/registration-claims")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/media/images/{bImage}/content")).StatusCode);
        // An unrelated unique failure must roll back, never masquerade as a saved claim.
        await using (var db = fixture.Open()) await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX fixture_unrelated_unique_claim_plate ON vehicle_registration_claims(normalized_plate);
            """);
        var cImage = await Upload();
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync("/api/vehicles", new { plate = "51A12345", vehicleType = "CAR", imageAssetId = cImage })).StatusCode);
        Assert.Equal(0, (await Data("/api/vehicles/registration-claims")).GetArrayLength());
        await using (var db = fixture.Open())
        {
            Assert.Single(await db.VehicleRegistrationClaims.ToArrayAsync());
            var stored = await db.RegisteredVehicles.SingleAsync();
            Assert.Equal(incumbent, stored.Id); Assert.Equal(a.Accounts.Single().Id, stored.AccountId);
            Assert.Null((await db.MediaAssets.SingleAsync(m => m.Id == cImage)).AttachedAt);
        }
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task TwoDriverAccountsForOneUserStayIsolatedThroughHttp()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var user = Driver("two-driver-accounts@example.test");
        await using (var db = fixture.Open()) { db.Users.Add(user); await db.SaveChangesAsync(); }
        await using var app = await fixture.StartHostAsync(configure: b => MediaTestSupport.Configure(b, new FakeMediaProvider()));
        using var client = app.GetTestClient();
        await MediaAssetTests.LoginAsync(client, user.Email!);
        var other = new SmartParking.UserService.Domain.Entities.Account { Id = Guid.NewGuid(), UserId = user.Id!.Value, RoleCode = "DRIVER" };
        var vehicle = new SmartParking.UserService.Domain.Entities.RegisteredVehicle {
            Id = Guid.NewGuid(), AccountId = other.Id, RawPlate = "51A12345", CanonicalPlate = "51A12345", VehicleType = "CAR",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var ownBody = Vehicle();
        await using (var db = fixture.Open())
        {
            db.Accounts.Add(other); db.RegisteredVehicles.Add(vehicle); await db.SaveChangesAsync();
            await MediaTestSupport.SeedAsync(db, user, ownBody);
        }
        async Task<JsonElement> Data(string route) => (await (await client.GetAsync(route)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(0, (await Data("/api/vehicles")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/vehicles/{vehicle.Id}", ownBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/vehicles", ownBody)).StatusCode);
        Assert.Equal(1, (await Data("/api/vehicles/registration-claims")).GetArrayLength());
        await using (var db = fixture.Open())
        {
            Assert.Equal(user.Accounts.Single().Id, (await db.VehicleRegistrationClaims.SingleAsync()).AccountId);
            Assert.Equal(other.Id, (await db.RegisteredVehicles.SingleAsync()).AccountId);
        }
        var fresh = Vehicle("51B54321");
        await using (var db = fixture.Open()) await MediaTestSupport.SeedAsync(db, user, fresh);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/vehicles", fresh)).StatusCode);
        var own = (await Data("/api/vehicles"))[0];
        Assert.Equal("51B54321", own.GetProperty("canonicalPlate").GetString());
        Assert.Equal(1, (await Data("/api/vehicles")).GetArrayLength());
    }
}
