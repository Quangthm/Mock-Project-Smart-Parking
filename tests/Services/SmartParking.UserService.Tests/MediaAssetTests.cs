using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Media;
using UserService.Application.Common.Models.Exceptions;
using UserService.Infrastructure.Media;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class MediaAssetTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Png, "image/png")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public void DecoderAcceptsRealImagesAndRejectsDisguisedOrTruncatedContent(SKEncodedImageFormat format, string mime)
    {
        var inspector = new SkiaImageInspector();
        var bytes = MediaTestSupport.Image(format);
        Assert.Equal(mime, inspector.Inspect(bytes, default).MimeType);
        Assert.Throws<AuthException>(() => inspector.Inspect("<html>not an image</html>"u8.ToArray(), default));
        Assert.Throws<AuthException>(() => inspector.Inspect(bytes[..12], default));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => inspector.Inspect(bytes, cancelled.Token));
    }

    [Fact]
    public void ExactFiveMiBBoundaryAndPixelLimitAreEnforcedBeforeAllocation()
    {
        var bytes = MediaTestSupport.Image();
        var exact = new byte[UploadImageValidator.MaximumBytes];
        bytes.CopyTo(exact, 0);
        var inspector = new SkiaImageInspector();
        Assert.Equal(exact.Length, inspector.Inspect(exact, default).ByteCount);
        Assert.Throws<AuthException>(() => inspector.Inspect(new byte[exact.Length + 1], default));
        using var bitmap = new SKBitmap(8193, 1);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        Assert.Throws<AuthException>(() => inspector.Inspect(encoded.ToArray(), default));
    }

    [Fact]
    public void AnimatedPngAndTotalPixelLimitAreRejected()
    {
        // Two-frame, one-pixel APNG with valid sequence numbers and CRCs.
        var animated = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAACGFjVEwAAAACAAAAAPONk3AAAAAaZmNUTAAAAAAAAAABAAAAAQAAAAAAAAAAAAEACgAAWn8w0AAAAA1JREFUeJxj+M/A8B8ABQAB/4mZPR0AAAAaZmNUTAAAAAEAAAABAAAAAQAAAAAAAAAAAAEACgAAwQzaBAAAABFmZEFUAAAAAnicY2D4z/AfAAQBAf9i5+mcAAAAAElFTkSuQmCC");
        var inspector = new SkiaImageInspector();
        Assert.Throws<AuthException>(() => inspector.Inspect(animated, default));
        using var bitmap = new SKBitmap(5001, 5000);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        Assert.Throws<AuthException>(() => inspector.Inspect(encoded.ToArray(), default));
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task HttpByteBoundariesFormatsDisabledAndProviderFailureUseSafeEnvelope()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var driver = Driver("media-boundaries@example.test");
        await using (var db = fixture.Open()) { db.Users.Add(driver); await db.SaveChangesAsync(); }
        var provider = new FakeMediaProvider();
        await using (var app = await fixture.StartHostAsync(configure: b => MediaTestSupport.Configure(b, provider)))
        {
            using var client = app.GetTestClient(); await LoginAsync(client, driver.Email!);
            foreach (var format in new[] { SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Png, SKEncodedImageFormat.Webp })
                Assert.Equal(HttpStatusCode.OK, (await UploadAsync(client, MediaTestSupport.Image(format), format.ToString(), avatar: true)).StatusCode);
            var exact = new byte[UploadImageValidator.MaximumBytes]; MediaTestSupport.Image().CopyTo(exact, 0);
            var accepted = await UploadAsync(client, exact, "exact", avatar: true);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var id = await AssetId(accepted);
            foreach (var invalid in new[] { new byte[exact.Length + 1], "<html>not-image</html>"u8.ToArray() })
            {
                var rejected = await UploadAsync(client, invalid, Guid.NewGuid().ToString(), avatar: true);
                Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
                Assert.Equal("MEDIA_INVALID_IMAGE", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            }
            provider.FailReads = true;
            var unavailable = await client.GetAsync($"/api/media/images/{id}/content");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
            Assert.Equal("MEDIA_UNAVAILABLE", (await unavailable.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        await using var disabled = await fixture.StartHostAsync(configure: b => {
            MediaTestSupport.Configure(b, provider);
            b.Services.AddSingleton(new MediaSettings(false, MediaTestSupport.Environment));
        });
        using var disabledClient = disabled.GetTestClient(); await LoginAsync(disabledClient, driver.Email!);
        var result = await UploadAsync(disabledClient, MediaTestSupport.Image(), "disabled", avatar: true);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        Assert.Equal("MEDIA_DISABLED", (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public void DraftExpiryAttachmentReplayClaimFencingAndIndependentHolds()
    {
        var asset = MediaTestSupport.Asset(Driver("domain@example.test"));
        var admitted = asset.AdmittedAt;
        var claim = Guid.NewGuid();
        Assert.True(asset.TryClaim(admitted, claim));
        Assert.False(asset.TryClaim(admitted, Guid.NewGuid()));
        Assert.False(asset.Complete(Guid.NewGuid(), "provider", "1", admitted));
        Assert.True(asset.Complete(claim, "provider", "1", admitted));
        Assert.True(asset.Expired(admitted.AddHours(24)));
        var vehicleId = Guid.NewGuid();
        asset.Attach(vehicleId, admitted.AddHours(23));
        Assert.False(asset.Expired(admitted.AddHours(24)));
        Assert.True(asset.Expired(admitted.AddDays(30)));
        Assert.Equal(admitted, asset.RetentionAnchor);
        Assert.False(asset.CanAttach(admitted.AddHours(23), Guid.NewGuid()));
        var first = new MediaHold(); var second = new MediaHold();
        first.Acknowledge(admitted, false); second.Acknowledge(admitted, false);
        first.Release(admitted.AddDays(1));
        Assert.False(first.Active); Assert.True(second.Active);
        Assert.Throws<InvalidOperationException>(() => new MediaHold().Acknowledge(admitted, true));
    }

    [Fact]
    public void DisabledConfigurationNeedsNoSecretsAndEnabledErrorsContainOnlySettingNames()
    {
        var services = new ServiceCollection();
        services.AddUserMedia(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<MediaSettings>().Enabled);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["MediaStorage:Enabled"] = "true", ["MediaStorage:Cloudinary:ApiKey"] = "must-not-appear" }).Build();
        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddUserMedia(config));
        Assert.Contains("CloudName", error.Message); Assert.Contains("ApiSecret", error.Message);
        Assert.DoesNotContain("must-not-appear", error.Message);
    }

    internal static User Driver(string email) => new()
    {
        Id = Guid.NewGuid(), Email = email, FullName = "Media Driver", Status = UserStatus.Active,
        PasswordHash = new BcryptPasswordService().Hash("Password@123"),
        Accounts = [new Account { Id = Guid.NewGuid(), RoleCode = "DRIVER" }]
    };
    internal static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password@123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new("Bearer", data.GetProperty("accessToken").GetString());
    }
    internal static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string key,
        Guid? vehicleId = null, Guid? draft = null, bool avatar = false)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent(bytes), "file", "untrusted-name.txt");
        if (vehicleId is { } vehicle) body.Add(new StringContent(vehicle.ToString()), "vehicleId");
        if (draft is { } intent) body.Add(new StringContent(intent.ToString()), "draftIntentId");
        using var request = new HttpRequestMessage(HttpMethod.Post, avatar ? "/api/auth/me/avatar/images" : "/api/vehicles/images") { Content = body };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
    internal static async Task<Guid> AssetId(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("assetId").GetGuid();

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task RealHostUploadReadAttachIdempotencyForeignPurposeAndExpiry()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var driver = Driver("media-owner@example.test"); var foreign = Driver("media-foreign@example.test");
        var vehicle = new RegisteredVehicle { Id = Guid.NewGuid(), AccountId = driver.Accounts.Single().Id,
            RawPlate = "51A12345", CanonicalPlate = "51A12345", VehicleType = "CAR", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        await using (var db = fixture.Open()) { db.Users.AddRange(driver, foreign); db.RegisteredVehicles.Add(vehicle); await db.SaveChangesAsync(); }
        var provider = new FakeMediaProvider();
        await using var app = await fixture.StartHostAsync(configure: b => MediaTestSupport.Configure(b, provider));
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/media/images/{Guid.NewGuid()}/content")).StatusCode);
        await LoginAsync(client, driver.Email!);
        var bytes = MediaTestSupport.Image();
        var upload = await UploadAsync(client, bytes, "upload-1", vehicle.Id);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var assetId = await AssetId(upload);
        Assert.Equal(assetId, await AssetId(await UploadAsync(client, bytes, "upload-1", vehicle.Id)));
        Assert.Equal(1, provider.Uploads);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(client, MediaTestSupport.Image(SKEncodedImageFormat.Jpeg), "upload-1", vehicle.Id)).StatusCode);
        var content = await client.GetAsync($"/api/media/images/{assetId}/content");
        Assert.Equal(bytes, await content.Content.ReadAsByteArrayAsync());
        Assert.Contains("no-store", content.Headers.CacheControl!.ToString());
        Assert.Contains("private", content.Headers.CacheControl.ToString());
        Assert.Equal("nosniff", content.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}/image", new { assetId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}/image", new { assetId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/auth/me/avatar", new { assetId })).StatusCode);
        var replacementId = await AssetId(await UploadAsync(client, bytes, "replacement", vehicle.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}/image", new { assetId = replacementId })).StatusCode);
        await LoginAsync(client, foreign.Email!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/media/images/{assetId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}/image", new { assetId })).StatusCode);
        await LoginAsync(client, driver.Email!);
        // Move all policy timestamps together; the database enforces their relationship.
        await using (var db = fixture.Open())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_assets SET admitted_at=admitted_at-interval '31 days', retention_anchor=retention_anchor-interval '31 days', retain_until=retain_until-interval '31 days', draft_expires_at=draft_expires_at-interval '31 days' WHERE id={assetId}");
        Assert.Equal(HttpStatusCode.Gone, (await client.GetAsync($"/api/media/images/{assetId}/content")).StatusCode);
        await using (var db = fixture.Open())
        {
            Assert.Equal(2, await db.MediaAssets.CountAsync());
            Assert.Equal(assetId, (await db.RegisteredVehicles.SingleAsync()).ImageAssetId);
            Assert.Equal(vehicle.AccountId, (await db.RegisteredVehicles.SingleAsync()).AccountId);
        }
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task LostProviderResponseRecoversAfterHostRestartWithoutDuplicateUpload()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var driver = Driver("media-recovery@example.test");
        await using (var db = fixture.Open()) { db.Users.Add(driver); await db.SaveChangesAsync(); }
        var provider = new FakeMediaProvider { LoseUploadResponse = true };
        Guid id;
        await using (var first = await fixture.StartHostAsync(configure: b => MediaTestSupport.Configure(b, provider)))
        {
            using var client = first.GetTestClient(); await LoginAsync(client, driver.Email!);
            var response = await UploadAsync(client, MediaTestSupport.Image(), "unknown", draft: Guid.NewGuid());
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); id = await AssetId(response);
        }
        await using (var db = fixture.Open()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_assets SET retry_at=NULL WHERE id={id}");
        provider.LoseUploadResponse = false;
        await using var second = await fixture.StartHostAsync(configure: b => MediaTestSupport.Configure(b, provider));
        using (var scope = second.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<MediaUseCases>().RecoverAsync(default);
        await using (var db = fixture.Open())
        {
            var asset = await db.MediaAssets.SingleAsync(); Assert.Equal("ready", asset.State);
            Assert.Null(asset.AttachedAt); Assert.Equal(1, provider.Uploads);
        }
    }
}
