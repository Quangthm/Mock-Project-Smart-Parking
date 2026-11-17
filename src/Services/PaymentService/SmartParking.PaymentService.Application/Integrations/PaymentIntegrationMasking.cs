namespace SmartParking.PaymentService.Application.Integrations;

/// <summary>
/// Secret masking helper. Never returns a raw credential value.
/// </summary>
internal static class PaymentIntegrationMasking
{
    /// <summary>
    /// Masks a client id: null/empty -&gt; null; length &gt; 4 -&gt; "****" + last 4; length &lt;= 4 -&gt; "****".
    /// </summary>
    public static string? MaskClientId(string? clientId)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        return clientId.Length > 4
            ? "****" + clientId[^4..]
            : "****";
    }
}
