using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

namespace SmartParking.FacilityService.Tests.TestDoubles;

internal sealed class FakeUnitOfWorkTransaction(List<string> calls) : IUnitOfWorkTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        calls.Add("commit");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
