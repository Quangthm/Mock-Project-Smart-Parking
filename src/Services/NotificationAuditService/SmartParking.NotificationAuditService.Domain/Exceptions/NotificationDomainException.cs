namespace SmartParking.NotificationAuditService.Domain.Exceptions;

public class NotificationDomainException : Exception
{
    public NotificationDomainException(string message) : base(message)
    {
    }

    public NotificationDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
