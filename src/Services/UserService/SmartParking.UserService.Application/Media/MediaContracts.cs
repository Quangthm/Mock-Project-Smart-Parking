using MediatR;
using FluentValidation;
using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Media;

public sealed record MediaSettings(bool Enabled, string Environment);
public sealed record ImageFacts(string MimeType, int Width, int Height, long ByteCount, string Sha256);
public interface IImageInspector { ImageFacts Inspect(byte[] content, CancellationToken ct); }
public sealed record ProviderImage(string PublicId, string AssetId, string Version, string MimeType,
    long ByteCount, string Sha256, int Width, int Height);
public interface IMediaProvider
{
    Task<ProviderImage?> FindAsync(MediaAsset asset, CancellationToken ct);
    Task<ProviderImage> UploadAsync(MediaAsset asset, byte[] content, CancellationToken ct);
    Task<Stream> OpenAsync(MediaAsset asset, CancellationToken ct);
}
public sealed record UploadImageCommand(string Purpose, byte[] Content, string IdempotencyKey,
    Guid? VehicleId, Guid? DraftIntentId) : IRequest<MediaAssetResponse>;
public sealed record GetImageQuery(Guid AssetId) : IRequest<MediaContent>;
public sealed record GetImageStatusQuery(Guid AssetId) : IRequest<MediaAssetResponse>;
public sealed record AttachImageCommand(string Purpose, Guid? VehicleId, Guid AssetId, Guid? ExpectedAssetId) : IRequest<MediaAssetResponse>;
public sealed record AttachImageRequest(Guid AssetId, Guid? ExpectedAssetId);
public sealed record MediaAssetResponse(Guid AssetId, string State, DateTimeOffset ExpiresAt,
    string? ContentPath, string StatusPath, string? RecoveryCode)
{
    public static MediaAssetResponse From(MediaAsset asset) => new(asset.Id, asset.State,
        asset.AttachedAt is null ? asset.DraftExpiresAt : asset.RetainUntil,
        asset.State == "ready" ? $"/api/media/images/{asset.Id}/content" : null,
        $"/api/media/images/{asset.Id}", asset.RecoveryCode);
}
public sealed record MediaContent(Stream Stream, string MimeType);
public sealed class UploadImageValidator : AbstractValidator<UploadImageCommand>
{
    public const int MaximumBytes = 5 * 1024 * 1024;
    public UploadImageValidator()
    {
        RuleFor(x => x.Purpose).Must(p => p is "avatar" or "vehicle");
        RuleFor(x => x.Content).NotNull().Must(b => b is { Length: > 0 and <= MaximumBytes });
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128).Matches("^[A-Za-z0-9._:-]+$");
        RuleFor(x => x).Must(x => x.Purpose == "avatar" ? x.VehicleId is null && x.DraftIntentId is null
            : (x.VehicleId is not null) != (x.DraftIntentId is not null))
            .WithMessage("Vehicle uploads require exactly one vehicle or draft intent; avatars have neither.");
        RuleFor(x => x.VehicleId).Must(id => id is null || id != Guid.Empty);
        RuleFor(x => x.DraftIntentId).Must(id => id is null || id != Guid.Empty);
    }
}
