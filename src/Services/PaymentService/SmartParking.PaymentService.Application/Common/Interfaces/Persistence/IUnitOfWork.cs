namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<T> WithLocksAsync<T>(Guid[] identities, Func<Task<T>> action, CancellationToken ct);
    Task<DateTimeOffset> DatabaseNowAsync(CancellationToken ct);
}
