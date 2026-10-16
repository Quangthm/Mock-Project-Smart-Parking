using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

public interface IReservationPricingSnapshotPort
{
    Task<PricingPolicySnapshot?> GetAcceptedPolicySnapshotAsync(
        Guid tenantId,
        Guid siteId,
        Guid reservationId,
        CancellationToken cancellationToken = default);
}
