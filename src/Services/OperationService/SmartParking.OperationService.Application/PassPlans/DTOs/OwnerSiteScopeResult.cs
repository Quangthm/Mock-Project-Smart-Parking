namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record OwnerSiteScopeResult(
    Guid UserId,
    Guid TenantId,
    Guid SiteId);
