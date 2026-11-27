using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.PassPlans;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed class GetPassRevenueSummaryQueryHandler(
    IOwnerSiteAccessPort siteAccessPort,
    IPassRevenueRepository revenueRepository) : IRequestHandler<GetPassRevenueSummaryQuery, PassRevenueSummaryDto>
{
    public async Task<PassRevenueSummaryDto> Handle(GetPassRevenueSummaryQuery query, CancellationToken ct)
    {
        await siteAccessPort.ValidateOwnerSiteAsync(query.SiteId, ct);

        if (!string.IsNullOrWhiteSpace(query.ProductType) &&
            string.Equals(query.ProductType.Trim(), "YEARLY", StringComparison.OrdinalIgnoreCase))
        {
            return new PassRevenueSummaryDto(
                query.SiteId,
                "VND",
                0m,
                0m,
                0m,
                0m,
                0m,
                0m,
                new PassRevenueCountsDto(0, 0, 0));
        }

        return await revenueRepository.GetSitePassRevenueSummaryAsync(
            query.SiteId,
            query.From,
            query.To,
            ct);
    }
}
