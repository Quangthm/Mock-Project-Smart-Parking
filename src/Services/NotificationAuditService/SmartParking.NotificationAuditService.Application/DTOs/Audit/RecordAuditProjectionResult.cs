namespace SmartParking.NotificationAuditService.Application.DTOs.Audit;

public record RecordAuditProjectionResult
{
    public Guid? ProjectionId { get; init; }
    public Guid EventId { get; init; }
    public bool IsSuccess { get; init; }
    public bool IsDuplicate { get; init; }
    public string? Message { get; init; }

    public static RecordAuditProjectionResult Success(Guid projectionId, Guid eventId) => new()
    {
        ProjectionId = projectionId,
        EventId = eventId,
        IsSuccess = true,
        IsDuplicate = false,
        Message = "Audit event successfully projected."
    };

    public static RecordAuditProjectionResult Deduplicated(Guid eventId) => new()
    {
        ProjectionId = null,
        EventId = eventId,
        IsSuccess = true,
        IsDuplicate = true,
        Message = "Duplicate audit event detected and safely ignored."
    };

    public static RecordAuditProjectionResult Failure(Guid eventId, string error) => new()
    {
        ProjectionId = null,
        EventId = eventId,
        IsSuccess = false,
        IsDuplicate = false,
        Message = error
    };
}
