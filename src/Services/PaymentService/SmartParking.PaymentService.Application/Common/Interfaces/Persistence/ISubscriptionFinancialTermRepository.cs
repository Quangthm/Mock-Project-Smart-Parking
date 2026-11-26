using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface ISubscriptionFinancialTermRepository
{
    Task<IPaymentDbTransaction?> BeginTransactionAsync(Guid siteId, Guid planId, CancellationToken ct = default);
    Task<SubscriptionFinancialTerm?> GetBySiteAndPlanAsync(Guid siteId, Guid planId, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionFinancialTerm>> GetActiveBySiteAsync(Guid siteId, Guid? planId, CancellationToken ct = default);
    Task<SubscriptionFinancialTermRevision?> GetRevisionByIdempotencyKeyAsync(Guid financialTermId, string idempotencyKey, CancellationToken ct = default);
    Task<SubscriptionFinancialTermRevision?> GetRevisionAsync(Guid financialTermId, int revisionNumber, CancellationToken ct = default);
    Task AddTermAsync(SubscriptionFinancialTerm term, CancellationToken ct = default);
    Task UpdateTermAsync(SubscriptionFinancialTerm term, CancellationToken ct = default);
    Task AddRevisionAsync(SubscriptionFinancialTermRevision revision, CancellationToken ct = default);
    Task UpdateRevisionAsync(SubscriptionFinancialTermRevision revision, CancellationToken ct = default);
    Task AddSubscriptionPolicyRevisionAsync(
        Guid financialTermId,
        Guid siteId,
        Guid planId,
        int planRevision,
        int financialTermRevision,
        string productType,
        string currency,
        IReadOnlyList<SubscriptionVehiclePrice> vehiclePrices,
        SubscriptionRefundPolicy refundPolicy,
        DateTimeOffset effectiveFrom,
        Guid createdBy,
        CancellationToken ct = default);
    Task CloseActivePolicyRevisionsAsync(Guid policyId, DateTimeOffset effectiveTo, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
