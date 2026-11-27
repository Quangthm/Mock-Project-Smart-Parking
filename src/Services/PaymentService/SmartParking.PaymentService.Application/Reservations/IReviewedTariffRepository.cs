using SmartParking.PaymentService.Domain.Pricing;
namespace SmartParking.PaymentService.Application.Reservations;

public interface IReviewedTariffRepository
{
    Task<(Guid RevisionId, PricingPolicySnapshot Policy)?> FindAsync(Guid tenantId, Guid siteId, CancellationToken ct);
}
