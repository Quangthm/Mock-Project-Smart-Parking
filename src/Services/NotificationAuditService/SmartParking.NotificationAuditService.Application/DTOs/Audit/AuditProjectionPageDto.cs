namespace SmartParking.NotificationAuditService.Application.DTOs.Audit;

public sealed record AuditProjectionPageDto(
    IReadOnlyList<AuditProjectionDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    AuditReadModelMetadataDto ReadModel);
