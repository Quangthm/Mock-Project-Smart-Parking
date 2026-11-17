using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;

namespace SmartParking.PaymentService.Application.Integrations;

public sealed class UpdatePaymentIntegrationCommandHandler
    : IRequestHandler<UpdatePaymentIntegrationCommand, UpdatePaymentIntegrationResult>
{
    private static readonly HashSet<string> AllowedModes =
        new(StringComparer.OrdinalIgnoreCase) { "LIVE", "SANDBOX", "FIXTURE" };

    private const string ValidationFailedCode = "VALIDATION_FAILED";
    private const string ValidationFailedMessage = "The request payload failed one or more validation rules.";
    private const string InvalidBaseUrlCode = "INVALID_PAYMENT_BASE_URL";

    // Verbatim message from the SPARK-340 API design error catalogue (§4).
    private const string InvalidBaseUrlMessage =
        "baseUrl truyền vào không thuộc Allowlist đã duyệt cho mode hiện tại.";

    private readonly IPaymentIntegrationSettings _settings;
    private readonly ICurrentActor _currentActor;
    private readonly ILogger<UpdatePaymentIntegrationCommandHandler> _logger;

    public UpdatePaymentIntegrationCommandHandler(
        IPaymentIntegrationSettings settings,
        ICurrentActor currentActor,
        ILogger<UpdatePaymentIntegrationCommandHandler> logger)
    {
        _settings = settings;
        _currentActor = currentActor;
        _logger = logger;
    }

    public Task<UpdatePaymentIntegrationResult> Handle(
        UpdatePaymentIntegrationCommand request,
        CancellationToken cancellationToken)
    {
        var mode = request.Mode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!AllowedModes.Contains(mode))
        {
            throw new PaymentIntegrationException(
                ValidationFailedCode,
                ValidationFailedMessage,
                new[] { new ApiFieldError("mode", "Mode must be one of LIVE, SANDBOX, or FIXTURE.") });
        }

        var allowedBaseUrls = _settings.AllowedBaseUrls(mode);
        if (!allowedBaseUrls.Contains(request.BaseUrl, StringComparer.OrdinalIgnoreCase))
        {
            throw new PaymentIntegrationException(
                InvalidBaseUrlCode,
                InvalidBaseUrlMessage);
        }

        var credentialErrors = new List<ApiFieldError>();
        ValidateCredential("clientId", request.ClientId, credentialErrors);
        ValidateCredential("apiKey", request.ApiKey, credentialErrors);
        ValidateCredential("checksumKey", request.ChecksumKey, credentialErrors);
        if (credentialErrors.Count > 0)
        {
            throw new PaymentIntegrationException(
                ValidationFailedCode,
                ValidationFailedMessage,
                credentialErrors);
        }

        var saved = _settings.Save(mode, request.BaseUrl, request.ClientId, request.ApiKey, request.ChecksumKey);

        var data = new PaymentIntegrationUpdateDto(
            Provider: "PAYOS",
            Mode: saved.Mode,
            BaseUrl: saved.BaseUrl,
            IsConfigured: saved.IsConfigured,
            ClientIdMasked: PaymentIntegrationMasking.MaskClientId(saved.ClientId),
            ApiKeyConfigured: !string.IsNullOrWhiteSpace(saved.ApiKey),
            ChecksumKeyConfigured: !string.IsNullOrWhiteSpace(saved.ChecksumKey),
            Status: saved.Status,
            LastTestedAt: saved.LastTestedAt);

        var message = saved.Mode == "FIXTURE"
            ? "Payment integration settings updated successfully. Switched to FIXTURE mode (zero outbound HTTP)."
            : $"Payment integration settings updated successfully. Credentials saved for mode {saved.Mode}.";

        WriteAuditLog(request, saved, mode);

        return Task.FromResult(new UpdatePaymentIntegrationResult(message, data));
    }

    private static void ValidateCredential(string property, string? value, List<ApiFieldError> errors)
    {
        // null means "keep the stored value". A non-null but blank value is rejected.
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ApiFieldError(property, $"{property} must not be empty or whitespace when provided."));
        }
    }

    private void WriteAuditLog(
        UpdatePaymentIntegrationCommand request,
        PaymentIntegrationConfiguration saved,
        string mode)
    {
        var keysModified = new List<string>();
        if (request.ClientId is not null) keysModified.Add("ClientId");
        if (request.ApiKey is not null) keysModified.Add("ApiKey");
        if (request.ChecksumKey is not null) keysModified.Add("ChecksumKey");

        // Information-level structured audit event. Only key names and non-secret
        // metadata are recorded; raw credential values are never logged.
        _logger.LogInformation(
            "PAYMENT_INTEGRATION_UPDATED scope={Scope} configVersion={ConfigVersion} actorId={ActorId} " +
            "provider={Provider} mode={Mode} baseUrl={BaseUrl} keysModified={KeysModified} " +
            "timestamp={Timestamp} remoteIp={RemoteIp}",
            "PLATFORM",
            saved.ConfigVersion,
            _currentActor.UserId,
            "PAYOS",
            mode,
            saved.BaseUrl,
            keysModified,
            DateTimeOffset.UtcNow,
            request.RemoteIp);
    }
}
