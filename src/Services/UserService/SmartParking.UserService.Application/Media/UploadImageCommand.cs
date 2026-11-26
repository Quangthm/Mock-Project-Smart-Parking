using MediatR;

namespace UserService.Application.Media;

public sealed record UploadImageCommand(string Purpose, byte[] Content, string IdempotencyKey,
    Guid? VehicleId, Guid? DraftIntentId) : IRequest<MediaAssetResponse>;
