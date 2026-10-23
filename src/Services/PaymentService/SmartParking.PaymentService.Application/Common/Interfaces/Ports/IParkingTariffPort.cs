using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

public interface IParkingTariffPort
{
    Task<PricingPolicySnapshot?> GetActivePolicyAsync(
        Guid tenantId,
        Guid siteId,
        PricingVehicleType vehicleType,
        CancellationToken cancellationToken = default);
}
