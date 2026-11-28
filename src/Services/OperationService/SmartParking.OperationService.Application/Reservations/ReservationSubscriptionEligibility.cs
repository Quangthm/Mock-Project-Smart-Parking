using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ReservationSubscriptionEligibility : IReservationSubscriptionEligibility
{
    // The initial policy grants no coverage. Future entitlement read failures must propagate.
    public PlateCoverage Evaluate(string canonicalPlate, Guid tenantId, Guid siteId, ReservationInterval interval) =>
        new(false, "none-v1");
}
