namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationQuoteBinding(
    Guid AccountId, string CanonicalPlate, Guid TenantId, Guid SiteId, Guid RequestedSlotId,
    DateTimeOffset Start, DateTimeOffset End, string PaymentMethod, PlateCoverage Coverage,
    string FacilityVersion, Guid PlatformPolicyId, int PlatformPolicyVersion, Guid SitePolicyId, int SitePolicyVersion);
