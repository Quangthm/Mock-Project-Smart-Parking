using System.Security.Cryptography;
using System.Text;
using MediatR;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Authorization;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Application.Media;

public sealed class MediaUseCases(IMediaAssetRepository media, IUnitOfWork work, ICurrentAccessService access,
    IImageInspector inspector, IMediaProvider provider, MediaSettings settings, TimeProvider clock, IRegistrationClaimRepository claims, IVehicleRepository vehicles, IUserRepository users)
    : IRequestHandler<UploadImageCommand, MediaAssetResponse>, IRequestHandler<GetImageQuery, MediaContent>,
      IRequestHandler<GetImageStatusQuery, MediaAssetResponse>, IRequestHandler<AttachImageCommand, MediaAssetResponse>
{
    private void Enabled()
    {
        if (!settings.Enabled) throw new AuthException("MEDIA_DISABLED", "Media is disabled.", 503);
    }
    private static AuthException Missing() => new("MEDIA_NOT_FOUND", "Image was not found.", 404);
    private static AuthException Conflict() => new("MEDIA_CONFLICT", "Image changed; reload before retrying.", 409);
    private static void RequirePurpose(CurrentAccessContext current, string purpose)
    {
        if (purpose is not ("avatar" or "vehicle")) throw new AuthException("VALIDATION_FAILED", "Invalid image purpose.", 400);
        if (purpose == "vehicle" && current.Role != "driver") throw new AuthException("FORBIDDEN", "Driver account required.", 403);
    }
    private static void RequireOwner(MediaAsset asset, CurrentAccessContext current)
    {
        if (asset.UserId != current.User.Id || asset.Purpose == "vehicle" && asset.CreatorAccountId != current.Account.Id)
            throw Missing();
        RequirePurpose(current, asset.Purpose);
    }
    private void RequireUnexpired(MediaAsset asset)
    {
        if (asset.Expired(clock.GetUtcNow())) throw new AuthException("MEDIA_EXPIRED", "Image has expired.", 410);
        if (asset.Environment != settings.Environment) throw new AuthException("MEDIA_UNAVAILABLE", "Image storage is unavailable.", 503);
    }

    public async Task<MediaAssetResponse> Handle(UploadImageCommand request, CancellationToken ct)
    {
        Enabled();
        var current = await access.RequireAsync(ct);
        RequirePurpose(current, request.Purpose);
        var facts = inspector.Inspect(request.Content, ct);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{request.Purpose}|{request.VehicleId:N}|{request.DraftIntentId:N}|{facts.Sha256}")));
        MediaAsset asset;
        var claim = Guid.NewGuid();
        var claimed = false;
        await using (var transaction = await work.BeginTransactionAsync(ct))
        {
            current = await access.LockAsync(ct);
            RequirePurpose(current, request.Purpose);
            if (request.VehicleId is { } target && await vehicles.FindOwnedAsync(target, current.Account.Id!.Value, true, ct) is null)
                throw Missing();
            var existing = await media.FindByKeyAsync(current.Account.Id!.Value, request.Purpose, request.IdempotencyKey, ct);
            if (existing is not null)
            {
                asset = existing;
                if (asset.Fingerprint != fingerprint) throw new AuthException("MEDIA_IDEMPOTENCY_CONFLICT", "Idempotency key was already used for different input.", 409);
                RequireOwner(asset, current);
                RequireUnexpired(asset);
            }
            else
            {
                var now = clock.GetUtcNow();
                var id = Guid.NewGuid();
                asset = new MediaAsset
                {
                    Id = id, UserId = current.User.Id!.Value, CreatorAccountId = current.Account.Id!.Value,
                    ScopeId = current.Account.Id.Value, Purpose = request.Purpose, VehicleId = request.VehicleId,
                    DraftIntentId = request.DraftIntentId, IdempotencyKey = request.IdempotencyKey, Fingerprint = fingerprint,
                    MimeType = facts.MimeType, ByteCount = facts.ByteCount, Sha256 = facts.Sha256, Width = facts.Width, Height = facts.Height,
                    Environment = settings.Environment, PublicId = $"smartpark/user-access/{id:N}", AdmittedAt = now,
                    DraftExpiresAt = now.AddHours(24), RetentionAnchor = now, RetainUntil = now.AddDays(30)
                };
                media.Add(asset);
            }
            claimed = asset.TryClaim(clock.GetUtcNow(), claim);
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        if (claimed)
        {
            // Admission is durable before provider IO. Cancellation leaves a lease
            // which recovery can claim after expiry; request bytes are not persisted.
            try
            {
                var result = await provider.FindAsync(asset, ct) ?? await provider.UploadAsync(asset, request.Content, ct);
                Verify(asset, result);
                await FinalizeAsync(asset.Id, claim, result, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (MediaProviderException)
            {
                await DeferAsync(asset.Id, claim, "PROVIDER_OUTCOME_PENDING", ct);
            }
        }
        return MediaAssetResponse.From((await media.FindAsync(asset.Id, false, ct))!);
    }

    internal static void Verify(MediaAsset asset, ProviderImage result)
    {
        if (result.PublicId != asset.PublicId || string.IsNullOrWhiteSpace(result.AssetId) || string.IsNullOrWhiteSpace(result.Version)
            || result.MimeType != asset.MimeType || result.ByteCount != asset.ByteCount || result.Sha256 != asset.Sha256
            || result.Width != asset.Width || result.Height != asset.Height)
            throw new MediaProviderException();
    }

    private async Task FinalizeAsync(Guid id, Guid claim, ProviderImage image, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var asset = await media.FindAsync(id, true, ct) ?? throw Missing();
        if (asset.Complete(claim, image.AssetId, image.Version, clock.GetUtcNow())) await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    private async Task DeferAsync(Guid id, Guid claim, string code, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var asset = await media.FindAsync(id, true, ct) ?? throw Missing();
        asset.Defer(claim, clock.GetUtcNow(), code);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<MediaAsset> ReadableAsync(Guid id, CancellationToken ct)
    {
        Enabled();
        var current = await access.RequireAsync(ct);
        var asset = await media.FindAsync(id, false, ct) ?? throw Missing();
        RequireOwner(asset, current);
        RequireUnexpired(asset);
        if (asset.AttachedAt is not null)
        {
            if (asset.RegistrationClaimId is { } claimId)
            {
                if (!await claims.OwnsImageAsync(current.Account.Id!.Value, claimId, asset.Id, ct)) throw Missing();
                return asset;
            }
            var reference = asset.Purpose == "avatar"
                ? (await users.GetByIdWithRolesAsync(asset.UserId, ct))?.AvatarAssetId
                : (await vehicles.FindOwnedAsync(asset.VehicleId!.Value, current.Account.Id!.Value, false, ct))?.ImageAssetId;
            if (reference != asset.Id) throw Missing();
        }
        else if (asset.VehicleId is { } vehicle && await vehicles.FindOwnedAsync(vehicle, current.Account.Id!.Value, false, ct) is null)
            throw Missing();
        return asset;
    }
    public async Task<MediaAssetResponse> Handle(GetImageStatusQuery request, CancellationToken ct) =>
        MediaAssetResponse.From(await ReadableAsync(request.AssetId, ct));

    public async Task<MediaContent> Handle(GetImageQuery request, CancellationToken ct)
    {
        var asset = await ReadableAsync(request.AssetId, ct);
        if (asset.State != "ready") throw new AuthException("MEDIA_PENDING", "Image upload is pending.", 409);
        try { return new(await provider.OpenAsync(asset, ct), asset.MimeType); }
        catch (MediaProviderException) { throw new AuthException("MEDIA_UNAVAILABLE", "Image storage is unavailable.", 503); }
    }

    public async Task<MediaAssetResponse> Handle(AttachImageCommand request, CancellationToken ct)
    {
        Enabled();
        await using var transaction = await work.BeginTransactionAsync(ct);
        var current = await access.LockAsync(ct);
        RequirePurpose(current, request.Purpose);
        RegisteredVehicle? vehicle = null;
        User? user = null;
        if (request.Purpose == "vehicle")
            vehicle = request.VehicleId is { } vehicleId
                ? await vehicles.FindOwnedAsync(vehicleId, current.Account.Id!.Value, true, ct) ?? throw Missing() : throw Missing();
        else user = await users.GetByIdForUpdateAsync(current.User.Id!.Value, ct) ?? throw Missing();
        var asset = await media.FindAsync(request.AssetId, true, ct) ?? throw Missing();
        RequireOwner(asset, current);
        RequireUnexpired(asset);
        if (asset.Purpose != request.Purpose || !asset.CanAttach(clock.GetUtcNow(), request.VehicleId)) throw Conflict();
        var previous = vehicle is null ? user!.AvatarAssetId : vehicle.ImageAssetId;
        if (previous != asset.Id)
        {
            if (previous != request.ExpectedAssetId) throw Conflict();
            asset.Attach(request.VehicleId, clock.GetUtcNow());
            if (vehicle is null) user!.AvatarAssetId = asset.Id;
            else vehicle.AttachImage(asset.Id, clock.GetUtcNow());
            await work.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return MediaAssetResponse.From(asset);
    }

    // Recovery reconciles already admitted bytes only. It never impersonates a
    // user, attaches an asset, or uploads absent request content.
    public async Task RecoverAsync(CancellationToken ct)
    {
        if (!settings.Enabled) return;
        foreach (var id in await media.PendingAsync(clock.GetUtcNow(), 10, ct))
        {
            var claim = Guid.NewGuid();
            MediaAsset asset;
            await using (var transaction = await work.BeginTransactionAsync(ct))
            {
                asset = (await media.FindAsync(id, true, ct))!;
                if (asset is null || asset.Environment != settings.Environment || !asset.TryClaim(clock.GetUtcNow(), claim)) continue;
                await work.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            try
            {
                var image = await provider.FindAsync(asset, ct);
                if (image is null) await DeferAsync(id, claim, "RESUBMIT_REQUIRED", ct);
                else { Verify(asset, image); await FinalizeAsync(id, claim, image, ct); }
            }
            catch (MediaProviderException) { await DeferAsync(id, claim, "PROVIDER_OUTCOME_PENDING", ct); }
        }
    }
}

/// <summary>Deliberately carries no provider messages, URLs, credentials or raw response.</summary>
public sealed class MediaProviderException : Exception;
