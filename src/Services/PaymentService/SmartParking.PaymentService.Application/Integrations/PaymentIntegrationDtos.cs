namespace SmartParking.PaymentService.Application.Integrations;

public sealed record PaymentIntegrationCapabilities(
    bool SupportsVietQr,
    bool SupportsWebhook,
    bool SupportsDirectRefund);

/// <summary>Response payload for GET /api/v1/admin/integrations/payment.</summary>
public sealed record PaymentIntegrationQueryDto(
    string Provider,
    string Mode,
    string BaseUrl,
    bool IsConfigured,
    string? ClientIdMasked,
    bool ApiKeyConfigured,
    bool ChecksumKeyConfigured,
    string Status,
    DateTimeOffset? LastTestedAt,
    PaymentIntegrationCapabilities Capabilities);

/// <summary>Response payload for PUT /api/v1/admin/integrations/payment (no capabilities field).</summary>
public sealed record PaymentIntegrationUpdateDto(
    string Provider,
    string Mode,
    string BaseUrl,
    bool IsConfigured,
    string? ClientIdMasked,
    bool ApiKeyConfigured,
    bool ChecksumKeyConfigured,
    string Status,
    DateTimeOffset? LastTestedAt);

/// <summary>Result envelope returned by the PUT handler (message + data).</summary>
public sealed record UpdatePaymentIntegrationResult(
    string Message,
    PaymentIntegrationUpdateDto Data);
