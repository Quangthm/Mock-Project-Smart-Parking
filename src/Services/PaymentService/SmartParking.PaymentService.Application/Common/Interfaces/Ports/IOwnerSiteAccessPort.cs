using SmartParking.PaymentService.Application.DTOs.PassPlans;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

public interface IOwnerSiteAccessPort
{
    Task<OwnerSiteScopeResult> ValidateOwnerSiteAsync(Guid siteId, CancellationToken ct);
}
