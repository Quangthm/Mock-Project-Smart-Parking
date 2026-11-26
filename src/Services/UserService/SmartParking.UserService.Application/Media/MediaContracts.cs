using MediatR;
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
