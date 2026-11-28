namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface ISubscribedVehicleRepository
{
    Task<bool> IsCoveredAsync(Guid tenantId, Guid siteId, string plateNumber, DateTimeOffset at, CancellationToken ct);
}
