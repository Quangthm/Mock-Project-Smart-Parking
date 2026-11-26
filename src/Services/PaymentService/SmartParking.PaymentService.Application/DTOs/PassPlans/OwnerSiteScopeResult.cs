namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record OwnerSiteScopeResult(
    Guid UserId,
    Guid TenantId,
    Guid SiteId);
