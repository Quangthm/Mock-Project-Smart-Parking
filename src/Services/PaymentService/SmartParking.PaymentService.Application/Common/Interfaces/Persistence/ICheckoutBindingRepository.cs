namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface ICheckoutBindingRepository
{
    // The original, immutable binding wins on retry or concurrent initiation.
    Task StoreIfAbsentAsync(long orderCode, string protectedBinding, CancellationToken cancellationToken);
    Task<string?> ReadAsync(long orderCode, CancellationToken cancellationToken);
}
