namespace SmartParking.NotificationAuditService.Domain.Exceptions;

public class NotificationForbiddenException : Exception
{
    public NotificationForbiddenException(string message) : base(message)
    {
    }

    public NotificationForbiddenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
