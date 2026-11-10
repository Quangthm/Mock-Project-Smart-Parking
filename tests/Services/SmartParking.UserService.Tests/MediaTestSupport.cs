using UserService.Application.Usecase.Vehicles;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using SkiaSharp;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.DTOs;
using UserService.Application.Media;
using UserService.Infrastructure.Media;
using UserService.Persistence;

namespace SmartParking.UserService.Tests;

internal static class MediaTestSupport
{
    public const string Environment = "synthetic-media";
    public static byte[] Image(SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 100);
        return encoded.ToArray();
    }
    public static MediaAsset Asset(User user, Guid? id = null, string purpose = "vehicle", Guid? vehicleId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var facts = new SkiaImageInspector().Inspect(Image(), default);
        var assetId = id ?? Guid.NewGuid();
        return new MediaAsset
        {
            Id = assetId, UserId = user.Id!.Value, CreatorAccountId = user.Accounts.First().Id!.Value,
            ScopeId = user.Accounts.First().Id!.Value, Purpose = purpose,
            VehicleId = vehicleId, DraftIntentId = purpose == "vehicle" && vehicleId is null ? Guid.NewGuid() : null,
            IdempotencyKey = Guid.NewGuid().ToString("N"), Fingerprint = facts.Sha256,
            MimeType = facts.MimeType, ByteCount = facts.ByteCount, Sha256 = facts.Sha256,
            Width = facts.Width, Height = facts.Height, Environment = Environment,
            PublicId = $"smartpark/user-access/{assetId:N}", AdmittedAt = now,
            RetentionAnchor = now, RetainUntil = now.AddDays(30), DraftExpiresAt = now.AddHours(24)
        };
    }
    public static async Task SeedAsync(AppDbContext db, User user, SaveVehicleRequest body, Guid? vehicleId = null)
    {
        if (body.ImageAssetId is null || body.ImageAssetId == Guid.Empty || await db.MediaAssets.AnyAsync(a => a.Id == body.ImageAssetId)) return;
        var asset = Asset(user, body.ImageAssetId);
        var claim = Guid.NewGuid();
        asset.TryClaim(DateTimeOffset.UtcNow, claim);
        asset.Complete(claim, Guid.NewGuid().ToString("N"), "1", DateTimeOffset.UtcNow);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        if (vehicleId is { } target) body.ExpectedImageAssetId = await db.RegisteredVehicles.Where(v => v.Id == target).Select(v => v.ImageAssetId).SingleOrDefaultAsync();
    }
    public static void Configure(WebApplicationBuilder builder, IMediaProvider provider)
    {
        builder.Services.AddSingleton(new MediaSettings(true, Environment));
        builder.Services.AddSingleton(provider);
        foreach (var descriptor in builder.Services.Where(d => d.ServiceType == typeof(IHostedService)
            && d.ImplementationType == typeof(global::UserService.API.Services.MediaRecoveryWorker)).ToArray()) builder.Services.Remove(descriptor);
    }
}

internal sealed class FakeMediaProvider : IMediaProvider
{
    public ConcurrentDictionary<Guid, byte[]> Bytes { get; } = new();
    public bool LoseUploadResponse { get; set; }
    public bool FailReads { get; set; }
    public int Uploads;
    public Task<ProviderImage?> FindAsync(MediaAsset asset, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (FailReads) throw new MediaProviderException();
        if (!Bytes.TryGetValue(asset.Id, out var bytes)) return Task.FromResult<ProviderImage?>(null);
        var facts = new SkiaImageInspector().Inspect(bytes, ct);
        return Task.FromResult<ProviderImage?>(new(asset.PublicId, asset.Id.ToString("N"), "1", facts.MimeType,
            facts.ByteCount, facts.Sha256, facts.Width, facts.Height));
    }
    public async Task<ProviderImage> UploadAsync(MediaAsset asset, byte[] content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Bytes.TryAdd(asset.Id, content)) Interlocked.Increment(ref Uploads);
        if (LoseUploadResponse) throw new MediaProviderException();
        return (await FindAsync(asset, ct))!;
    }
    public Task<Stream> OpenAsync(MediaAsset asset, CancellationToken ct) =>
        FailReads ? throw new MediaProviderException() : Task.FromResult<Stream>(new MemoryStream(Bytes[asset.Id], false));
}
