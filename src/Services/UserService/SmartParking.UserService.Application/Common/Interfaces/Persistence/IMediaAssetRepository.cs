using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Persistence;

public interface IMediaAssetRepository
{
    Task<MediaAsset?> FindByKeyAsync(Guid scopeId, string purpose, string key, CancellationToken ct);
    Task<MediaAsset?> FindAsync(Guid id, bool forUpdate, CancellationToken ct);
    void Add(MediaAsset asset);
    Task<Guid[]> PendingAsync(DateTimeOffset now, int take, CancellationToken ct);
}
