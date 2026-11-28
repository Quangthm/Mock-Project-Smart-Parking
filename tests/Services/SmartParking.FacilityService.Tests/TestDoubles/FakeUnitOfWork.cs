using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

namespace SmartParking.FacilityService.Tests.TestDoubles;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public List<string> Calls { get; } = [];

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("begin");
        return Task.FromResult<IUnitOfWorkTransaction>(new FakeUnitOfWorkTransaction(Calls));
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("save");
        return Task.FromResult(1);
    }
}
