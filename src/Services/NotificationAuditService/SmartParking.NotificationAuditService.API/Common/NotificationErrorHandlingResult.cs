namespace SmartParking.NotificationAuditService.API.Common;

public readonly record struct NotificationErrorHandlingResult(NotificationErrorResponse Response, bool IsUnexpected = false);
