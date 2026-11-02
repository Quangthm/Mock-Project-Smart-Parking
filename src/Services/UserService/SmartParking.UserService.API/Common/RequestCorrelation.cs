namespace UserService.API.Common;

/// <summary>Opaque server-generated request ID, independent of incoming headers.</summary>
public static class RequestCorrelation
{
    private static readonly object Key = new();
    public static string Get(HttpContext context)
    {
        if (context.Items[Key] is string value) return value;
        var generated = Guid.NewGuid().ToString("N");
        context.Items[Key] = generated;
        return generated;
    }
}
