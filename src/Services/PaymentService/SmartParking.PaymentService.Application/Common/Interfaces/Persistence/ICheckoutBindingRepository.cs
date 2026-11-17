namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public sealed record CheckoutBindingProvenance(string Source, string? EvidenceReference = null, Guid? PaymentOrderId = null);

public interface ICheckoutBindingRepository
{
    // The original, immutable binding wins on retry or concurrent initiation.
    Task StoreIfAbsentAsync(long orderCode, string protectedBinding, CancellationToken cancellationToken,
        CheckoutBindingProvenance? provenance = null);
    Task<string?> ReadAsync(long orderCode, CancellationToken cancellationToken);
    Task<string?> ReadLegacyAsync(long orderCode, CancellationToken cancellationToken);
}
