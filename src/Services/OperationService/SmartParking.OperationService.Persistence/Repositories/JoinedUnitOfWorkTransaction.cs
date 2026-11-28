using Microsoft.EntityFrameworkCore.Storage;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Persistence.Repositories;

internal sealed class JoinedUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
