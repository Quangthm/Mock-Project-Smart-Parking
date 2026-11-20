using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Tests.TestDoubles;

public class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task<T> WithLocksAsync<T>(Guid[] identities, Func<Task<T>> action, CancellationToken ct) => action();
    public Task<DateTimeOffset> DatabaseNowAsync(CancellationToken ct) => Task.FromResult(DateTimeOffset.UtcNow);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}
