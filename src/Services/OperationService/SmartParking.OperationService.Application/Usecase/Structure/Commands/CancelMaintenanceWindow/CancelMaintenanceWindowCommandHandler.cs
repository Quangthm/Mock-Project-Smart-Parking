using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.CancelMaintenanceWindow;

public sealed class CancelMaintenanceWindowCommandHandler(
    IStructureRepository repository,
    IOperationsConcurrency guards,
    IUnitOfWork work,
    TimeProvider clock) : IRequestHandler<CancelMaintenanceWindowCommand>
{
    public async Task Handle(CancelMaintenanceWindowCommand request, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockSiteAsync(request.SiteId, ct);

        var existing = await repository.MaintenanceClosureAsync(request.WindowId, ct);
        if (existing is null)
            return;
        if (existing.TenantId != request.TenantId || existing.SiteId != request.SiteId)
            throw new HoldConflictException();

        if (existing.CancelledAt is null)
        {
            existing.CancelledAt = clock.GetUtcNow();
            await work.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}