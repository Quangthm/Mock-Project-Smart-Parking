using SmartParking.PaymentService.Application.DTOs.PassPlans;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface IPassRevenueRepository
{
    Task<PassRevenueSummaryDto> GetSitePassRevenueSummaryAsync(
        Guid siteId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);
}
