using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Ports;

public interface IPassPlanRepository
{
    Task<IReadOnlyList<ParkingPassPlan>> GetPlansBySiteAsync(Guid siteId, CancellationToken ct);
    Task<ParkingPassPlan?> GetPlanAsync(Guid siteId, PassProductType productType, CancellationToken ct);
    Task<ParkingPassPlan?> GetPlanByIdAsync(Guid siteId, Guid planId, CancellationToken ct);
    Task<ParkingPassPlanRevision?> GetRevisionAsync(Guid planId, int revisionNumber, CancellationToken ct);
    Task<ParkingPassPlanRevision?> GetRevisionByIdempotencyKeyAsync(Guid planId, string idempotencyKey, CancellationToken ct);
    Task<string> GetEffectiveTimeZoneAsync(Guid siteId, CancellationToken ct);
    Task<Guid?> GetLocalSiteTenantIdAsync(Guid siteId, CancellationToken ct);
    Task<IReadOnlySet<PassVehicleType>> GetLocalSupportedVehicleTypesAsync(Guid siteId, CancellationToken ct);
    Task<bool> HasLocalSiteEvidenceAsync(Guid siteId, CancellationToken ct);
    Task<IUnitOfWorkTransaction?> BeginSiteLockTransactionAsync(Guid siteId, CancellationToken ct);
    Task AddPlanAsync(ParkingPassPlan plan, CancellationToken ct);
    Task UpdatePlanAsync(ParkingPassPlan plan, CancellationToken ct);
    Task AddRevisionAsync(ParkingPassPlanRevision revision, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
