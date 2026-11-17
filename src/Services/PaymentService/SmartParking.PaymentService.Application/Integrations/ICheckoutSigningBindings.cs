using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

namespace SmartParking.PaymentService.Application.Integrations;

public interface ICheckoutSigningBindings
{
    // False means another signing configuration already owns this order code.
    Task<bool> BindAsync(long orderCode, PaymentConfigurationSnapshot configuration, CancellationToken cancellationToken,
        CheckoutBindingProvenance? provenance = null);
    Task<string?> ReadChecksumKeyAsync(long orderCode, CancellationToken cancellationToken);
    Task<bool?> MatchesAsync(long orderCode, PaymentConfigurationSnapshot configuration, CancellationToken cancellationToken);
    Task<bool?> MigrateLegacyAsync(long orderCode, string evidenceReference, bool apply, CancellationToken cancellationToken);
}
