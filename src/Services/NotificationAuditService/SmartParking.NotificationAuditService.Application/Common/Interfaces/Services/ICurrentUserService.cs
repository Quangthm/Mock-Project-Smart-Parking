namespace SmartParking.NotificationAuditService.Application.Common.Interfaces.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserRole { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin => string.Equals(UserRole, "Admin", StringComparison.OrdinalIgnoreCase);
}
