using FluentValidation;

namespace SmartParking.PaymentService.Application.Integrations;

public sealed class UpdatePaymentIntegrationCommandValidator : AbstractValidator<UpdatePaymentIntegrationCommand>
{
    private static readonly HashSet<string> AllowedModes =
        new(StringComparer.OrdinalIgnoreCase) { "LIVE", "SANDBOX", "FIXTURE" };

    public UpdatePaymentIntegrationCommandValidator()
    {
        RuleFor(x => x.Mode)
            .NotEmpty().WithMessage("Mode is required.")
            .Must(m => AllowedModes.Contains(m?.Trim() ?? string.Empty))
            .WithMessage("Mode must be one of LIVE, SANDBOX, or FIXTURE.");

        RuleFor(x => x.BaseUrl)
            .NotEmpty().WithMessage("BaseUrl is required.");

        RuleFor(x => x.ClientId)
            .Must(v => v is null || !string.IsNullOrWhiteSpace(v))
            .WithMessage("clientId must not be empty or whitespace when provided.");

        RuleFor(x => x.ApiKey)
            .Must(v => v is null || !string.IsNullOrWhiteSpace(v))
            .WithMessage("apiKey must not be empty or whitespace when provided.");

        RuleFor(x => x.ChecksumKey)
            .Must(v => v is null || !string.IsNullOrWhiteSpace(v))
            .WithMessage("checksumKey must not be empty or whitespace when provided.");
    }
}
