using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface IReservationRepository : IRepository<ReservationRow>
{
    Task<ReservationRow?> FindForUpdateAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken);
}
