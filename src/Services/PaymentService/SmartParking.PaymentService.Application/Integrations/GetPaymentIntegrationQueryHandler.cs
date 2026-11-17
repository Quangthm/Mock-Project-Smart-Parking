using MediatR;

namespace SmartParking.PaymentService.Application.Integrations;

public sealed class GetPaymentIntegrationQueryHandler
    : IRequestHandler<GetPaymentIntegrationQuery, PaymentIntegrationQueryDto>
{
    private readonly IPaymentIntegrationSettings _settings;

    public GetPaymentIntegrationQueryHandler(IPaymentIntegrationSettings settings)
    {
        _settings = settings;
    }

    public Task<PaymentIntegrationQueryDto> Handle(
        GetPaymentIntegrationQuery request,
        CancellationToken cancellationToken)
    {
        var current = _settings.GetCurrent();

        var dto = new PaymentIntegrationQueryDto(
            Provider: "PAYOS",
            Mode: current.Mode,
            BaseUrl: current.BaseUrl,
            IsConfigured: current.IsConfigured,
            ClientIdMasked: PaymentIntegrationMasking.MaskClientId(current.ClientId),
            ApiKeyConfigured: !string.IsNullOrWhiteSpace(current.ApiKey),
            ChecksumKeyConfigured: !string.IsNullOrWhiteSpace(current.ChecksumKey),
            Status: current.Status,
            LastTestedAt: current.LastTestedAt,
            Capabilities: new PaymentIntegrationCapabilities(
                SupportsVietQr: true,
                SupportsWebhook: true,
                SupportsDirectRefund: false));

        return Task.FromResult(dto);
    }
}
