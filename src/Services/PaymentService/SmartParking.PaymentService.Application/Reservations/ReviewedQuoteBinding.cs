namespace SmartParking.PaymentService.Application.Reservations;

public sealed record ReviewedQuoteBinding(Guid AccountId, string CanonicalPlate, Guid TenantId, Guid SiteId,
    Guid RequestedSlotId, DateTimeOffset Start, DateTimeOffset End, string PaymentMethod, QuoteCoverage Coverage,
    Guid PlatformPolicyId, int PlatformPolicyVersion, Guid SitePolicyId, int SitePolicyVersion);
