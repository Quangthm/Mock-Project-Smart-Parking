using MediatR;

namespace SmartParking.PaymentService.Application.Integrations;

/// <summary>
/// Updates the PayOS payment integration configuration. Credentials are stored
/// isolated per mode and are never echoed back to callers.
/// </summary>
/// <param name="Mode">Target mode: LIVE, SANDBOX or FIXTURE.</param>
/// <param name="BaseUrl">Must belong to the allowlist for the target mode.</param>
/// <param name="ClientId">New client id; <c>null</c> keeps the stored value.</param>
/// <param name="ApiKey">New api key; <c>null</c> keeps the stored value.</param>
/// <param name="ChecksumKey">New checksum key; <c>null</c> keeps the stored value.</param>
/// <param name="RemoteIp">Caller remote IP, used only for the redacted audit trail.</param>
public sealed record UpdatePaymentIntegrationCommand(
    string Mode,
    string BaseUrl,
    string? ClientId,
    string? ApiKey,
    string? ChecksumKey,
    string? RemoteIp = null) : IRequest<UpdatePaymentIntegrationResult>;
