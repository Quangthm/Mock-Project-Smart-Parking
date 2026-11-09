using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface IReservationRepository : IRepository<ReservationRow>
{
    Task<ReservationRow?> FindForUpdateAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReservationGateEntry>> ListAsync(Guid tenantId, Guid siteId, CancellationToken ct);
    Task<ReservationGateEntry?> FindAsync(Guid tenantId, Guid siteId, string query, CancellationToken ct);
    Task<ReservationGateEntry?> FindByIdAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken ct);
}
