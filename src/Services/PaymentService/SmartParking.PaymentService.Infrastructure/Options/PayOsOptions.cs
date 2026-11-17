namespace SmartParking.PaymentService.Infrastructure.Options;

public class PayOsOptions
{
    public const string SectionName = "PayOs";

    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional previous checksum key used as a webhook signature grace-window seed
    /// during key rotation. Never returned to API callers.
    /// </summary>
    public string PreviousChecksumKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";

    /// <summary>
    /// Active integration mode. One of LIVE, SANDBOX or FIXTURE (defaults to SANDBOX).
    /// </summary>
    public string Mode { get; set; } = "SANDBOX";

    // Mode-isolated credentials. Live and Sandbox credentials are stored separately
    // and are never copied across modes.
    public string LiveClientId { get; set; } = string.Empty;
    public string LiveApiKey { get; set; } = string.Empty;
    public string LiveChecksumKey { get; set; } = string.Empty;

    public string SandboxClientId { get; set; } = string.Empty;
    public string SandboxApiKey { get; set; } = string.Empty;
    public string SandboxChecksumKey { get; set; } = string.Empty;
}
