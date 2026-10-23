using SmartParking.ReservationService.Domain.Entities;

namespace SmartParking.ReservationService.Application.Common.Interfaces.Persistence;

public interface IReservationRepository : IRepository<ReservationRow>
{
    Task<ReservationRow?> FindForUpdateAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken);
}
