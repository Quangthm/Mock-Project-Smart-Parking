using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationSubscriptionEligibility
{
    PlateCoverage Evaluate(string canonicalPlate, Guid tenantId, Guid siteId, ReservationInterval interval);
}
