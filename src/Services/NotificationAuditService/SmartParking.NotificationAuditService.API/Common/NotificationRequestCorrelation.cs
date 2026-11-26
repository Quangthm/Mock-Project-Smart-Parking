namespace SmartParking.NotificationAuditService.API.Common;

public static class NotificationRequestCorrelation
{
    private static readonly object CorrelationKey = new();

    public static string Get(HttpContext context)
    {
        if (context.Items[CorrelationKey] is string id)
        {
            return id;
        }

        var generated = Guid.NewGuid().ToString("N");
        context.Items[CorrelationKey] = generated;
        return generated;
    }
}
