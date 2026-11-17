namespace SmartParking.PaymentService.Application.Integrations;

public interface ICheckoutSigningBindings
{
    // False means another signing configuration already owns this order code.
    Task<bool> BindAsync(long orderCode, PaymentConfigurationSnapshot configuration, CancellationToken cancellationToken);
    Task<string?> ReadChecksumKeyAsync(long orderCode, CancellationToken cancellationToken);
}
