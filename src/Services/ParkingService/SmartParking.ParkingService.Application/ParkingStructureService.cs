using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Application;

// The caller supplies the authenticated user ID, never an unverified request owner ID.
public sealed record OwnerScope(Guid UserId, Guid TenantId);
public interface IParkingStructureRepository
{
    Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken ct);
    Task<ParkingStructure> ReadAsync(OwnerScope owner, Guid siteId, CancellationToken ct);
    Task<SiteProfile> CreateAsync(OwnerScope owner, string code, string name, string address, decimal? latitude, decimal? longitude, CancellationToken ct);
    Task<T> ChangeAsync<T>(OwnerScope owner, Guid siteId, Func<ParkingStructure, T> change, CancellationToken ct);
}

public sealed class ParkingStructureService(IParkingStructureRepository repository)
{
    public Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken ct = default) => repository.ListAsync(owner, ct);
    public Task<ParkingStructure> GetAsync(OwnerScope owner, Guid siteId, CancellationToken ct = default) => repository.ReadAsync(owner, siteId, ct);
    public Task<SiteProfile> CreateAsync(OwnerScope owner, string code, string name, string address, decimal? latitude = null, decimal? longitude = null, CancellationToken ct = default)
        => repository.CreateAsync(owner, code, name, address, latitude, longitude, ct);
    public Task<Guid> AddUnitAsync(OwnerScope owner, Guid siteId, UnitType type, string name, Guid? parentId = null, int capacity = 0, CancellationToken ct = default)
        => repository.ChangeAsync(owner, siteId, s => s.AddUnit(type, name, parentId, capacity), ct);
    public Task<Guid> AddSlotAsync(OwnerScope owner, Guid siteId, Guid unitId, string code, VehicleType vehicle, SlotType type = SlotType.STANDARD, CancellationToken ct = default)
        => repository.ChangeAsync(owner, siteId, s => s.AddSlot(unitId, code, vehicle, type), ct);
    public Task<Guid> AddAccessPathAsync(OwnerScope owner, Guid siteId, string code, Guid? from, Guid? to, string? mapData = null, CancellationToken ct = default)
        => repository.ChangeAsync(owner, siteId, s => s.AddAccessPath(code, from, to, mapData), ct);
    public Task ChangeAsync(OwnerScope owner, Guid siteId, Action<ParkingStructure> change, CancellationToken ct = default)
        => repository.ChangeAsync(owner, siteId, s => { change(s); return true; }, ct);
}
