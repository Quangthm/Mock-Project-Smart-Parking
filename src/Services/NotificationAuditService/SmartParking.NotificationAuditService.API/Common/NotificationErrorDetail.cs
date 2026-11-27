namespace SmartParking.NotificationAuditService.API.Common;

public readonly record struct NotificationErrorDetail(string Field, string Message, string? Code = null);
