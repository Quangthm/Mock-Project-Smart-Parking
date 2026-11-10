using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Media;
using UserService.Persistence;

namespace SmartParking.UserService.Tests;

public sealed class MediaAssetConcurrencyTests
{
    [PostgresFact, Trait("Category", "Postgres")]
    public async Task CompetingReplacementsHaveOneWinnerAndSameUserOtherDriverAccountIsDenied()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var user = MediaAssetTests.Driver("media-race@example.test");
        var other = new Account { Id = Guid.NewGuid(), UserId = user.Id!.Value, RoleCode = "DRIVER" };
        var ownVehicle = new RegisteredVehicle { Id = Guid.NewGuid(), AccountId = user.Accounts.Single().Id,
            RawPlate = "51A12345", CanonicalPlate = "51A12345", VehicleType = "CAR", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        await using (var db = fixture.Open()) { db.Users.Add(user); db.RegisteredVehicles.Add(ownVehicle); await db.SaveChangesAsync(); }
        var fake = new FakeMediaProvider();
        await using var app = await fixture.StartHostAsync(configure: b => MediaTestSupport.Configure(b, fake));
        using var client = app.GetTestClient(); await MediaAssetTests.LoginAsync(client, user.Email!);
        // Add second Account after login, preserving the selected Account in the issued token.
        var otherAsset = MediaTestSupport.Asset(user);
        otherAsset.CreatorAccountId = other.Id!.Value; otherAsset.ScopeId = other.Id.Value;
        await using (var db = fixture.Open()) { db.Accounts.Add(other); db.MediaAssets.Add(otherAsset); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/media/images/{otherAsset.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/vehicles/{ownVehicle.Id}/image", new { assetId = otherAsset.Id })).StatusCode);
        var bytes = MediaTestSupport.Image();
        var first = await MediaAssetTests.AssetId(await MediaAssetTests.UploadAsync(client, bytes, "first", ownVehicle.Id));
        var second = await MediaAssetTests.AssetId(await MediaAssetTests.UploadAsync(client, bytes, "second", ownVehicle.Id));
        var outcomes = await Task.WhenAll(client.PutAsJsonAsync($"/api/vehicles/{ownVehicle.Id}/image", new { assetId = first }),
            client.PutAsJsonAsync($"/api/vehicles/{ownVehicle.Id}/image", new { assetId = second }));
        Assert.Single(outcomes, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(outcomes, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var check = fixture.Open();
        var attached = (await check.RegisteredVehicles.SingleAsync()).ImageAssetId;
        Assert.Contains(attached!.Value, new[] { first, second });
        Assert.Equal(1, await check.MediaAssets.CountAsync(a => a.AttachedAt != null));
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task AvatarIsUserScopedAndExpiredDraftCannotAttach()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var user = MediaAssetTests.Driver("avatar@example.test");
        await using (var db = fixture.Open()) { db.Users.Add(user); await db.SaveChangesAsync(); }
        await using var app = await fixture.StartHostAsync(configure: b => MediaTestSupport.Configure(b, new FakeMediaProvider()));
        using var client = app.GetTestClient(); await MediaAssetTests.LoginAsync(client, user.Email!);
        var id = await MediaAssetTests.AssetId(await MediaAssetTests.UploadAsync(client, MediaTestSupport.Image(), "avatar", avatar: true));
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/auth/me/avatar", new { assetId = id })).StatusCode);
        var profile = await client.GetStringAsync("/api/auth/me");
        Assert.Contains($"/api/media/images/{id}/content", profile);
        var draft = await MediaAssetTests.AssetId(await MediaAssetTests.UploadAsync(client, MediaTestSupport.Image(), "draft", avatar: true));
        await using (var db = fixture.Open())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_assets SET admitted_at=admitted_at-interval '25 hours', retention_anchor=retention_anchor-interval '25 hours', retain_until=retain_until-interval '25 hours', draft_expires_at=draft_expires_at-interval '25 hours' WHERE id={draft}");
        Assert.Equal(HttpStatusCode.Gone, (await client.GetAsync($"/api/media/images/{draft}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await client.PutAsJsonAsync("/api/auth/me/avatar", new { assetId = draft, expectedAssetId = id })).StatusCode);
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task ProviderSuccessThenDatabaseFailureKeepsRecoverableAdmission()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var user = MediaAssetTests.Driver("media-commit@example.test");
        await using (var db = fixture.Open()) { db.Users.Add(user); await db.SaveChangesAsync(); }
        var fake = new FakeMediaProvider(); var failure = new FailFinalization();
        await using var app = await fixture.StartHostAsync(configure: builder =>
        {
            MediaTestSupport.Configure(builder, fake);
            builder.Services.AddDbContext<AppDbContext>(o => o.AddInterceptors(failure));
        });
        using var client = app.GetTestClient(); await MediaAssetTests.LoginAsync(client, user.Email!);
        var result = await MediaAssetTests.UploadAsync(client, MediaTestSupport.Image(), "commit-failure", draft: Guid.NewGuid());
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.DoesNotContain("synthetic-finalize-failure", await result.Content.ReadAsStringAsync());
        await using (var db = fixture.Open())
        {
            var asset = await db.MediaAssets.SingleAsync(); Assert.Equal("pending", asset.State);
            Assert.Null(asset.AttachedAt); Assert.Equal(1, fake.Uploads);
            await db.Database.ExecuteSqlRawAsync("UPDATE media_assets SET lease_until=NULL");
        }
        using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<MediaUseCases>().RecoverAsync(default);
        await using (var db = fixture.Open()) Assert.Equal("ready", (await db.MediaAssets.SingleAsync()).State);
        Assert.Equal(1, fake.Uploads);
    }

    private sealed class FailFinalization : DbCommandInterceptor
    {
        private int failed;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("UPDATE media_assets", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Any(p => Equals(p.Value, "ready"))
                && Interlocked.Exchange(ref failed, 1) == 0) throw new InvalidOperationException("synthetic-finalize-failure");
            return base.ReaderExecutingAsync(command, eventData, result, ct);
        }
    }
}
