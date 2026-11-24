using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.ApplyMaintenanceWindow;

public sealed class ApplyMaintenanceWindowCommandHandler(
    IStructureRepository repository,
    IOperationsConcurrency guards,
    IUnitOfWork work,
    TimeProvider clock) : IRequestHandler<ApplyMaintenanceWindowCommand, MaintenanceAcknowledgement>
{
    public async Task<MaintenanceAcknowledgement> Handle(ApplyMaintenanceWindowCommand request, CancellationToken ct)
    {
        var start = request.Start.ToUniversalTime();
        var end = request.End.ToUniversalTime();
        var scopedUnitIds = request.ScopedUnitIds?.Distinct().ToArray() ?? [];
        var scopedSlotIds = request.ScopedSlotIds?.Distinct().ToArray() ?? [];

        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockSiteAsync(request.SiteId, ct);

        var state = await repository.StateAsync(request.SiteId, ct);
        if (state is not null && state.TenantId != request.TenantId)
            throw new HoldConflictException();

        var existing = await repository.MaintenanceClosureAsync(request.WindowId, ct);
        if (existing is not null)
        {
            if (existing.TenantId != request.TenantId || existing.SiteId != request.SiteId)
                throw new HoldConflictException();
            if (existing.CancelledAt is not null)
                throw new InvalidStructureEditException("Cancelled maintenance window cannot be re-applied.");

            var sameScope = request.UnitId == existing.UnitId
                && request.SlotId == existing.SlotId;
            var sameSnapshot = SameIds(existing.ScopedUnitIds, scopedUnitIds)
                && SameIds(existing.ScopedSlotIds, scopedSlotIds);
            if (sameScope && sameSnapshot && start == existing.WindowStart && end == existing.WindowEnd)
                return new MaintenanceAcknowledgement(existing.EnforcementRef, existing.AcknowledgedAt);
            if (!sameScope || start != existing.WindowStart || end < existing.WindowEnd)
                throw new InvalidStructureEditException(
                    "Maintenance window can only be extended; start and scope cannot change and end cannot shorten.");
        }

        var conflicts = await repository.FindMaintenanceConflictsAsync(
            request.TenantId,
            request.SiteId,
            request.UnitId,
            request.SlotId,
            scopedUnitIds.ToHashSet(),
            scopedSlotIds.ToHashSet(),
            start,
            end,
            clock.GetUtcNow(),
            ct);
        if (conflicts.Count > 0)
            throw new MaintenanceCommitmentConflictException(
                conflicts,
                $"{conflicts.Count} commitment(s) overlap the requested maintenance window.");

        var acknowledgedAt = clock.GetUtcNow();
        var enforcementRef = Guid.NewGuid();
        if (existing is null)
        {
            repository.AddMaintenanceClosure(new StructureMaintenanceClosureRow
            {
                WindowId = request.WindowId,
                TenantId = request.TenantId,
                SiteId = request.SiteId,
                UnitId = request.UnitId,
                SlotId = request.SlotId,
                ScopedUnitIds = Serialize(scopedUnitIds),
                ScopedSlotIds = Serialize(scopedSlotIds),
                WindowStart = start,
                WindowEnd = end,
                Reason = request.Reason,
                EnforcementRef = enforcementRef,
                AcknowledgedAt = acknowledgedAt
            });
        }
        else
        {
            existing.ScopedUnitIds = Serialize(scopedUnitIds);
            existing.ScopedSlotIds = Serialize(scopedSlotIds);
            existing.WindowEnd = end;
            existing.Reason = request.Reason;
            existing.EnforcementRef = enforcementRef;
            existing.AcknowledgedAt = acknowledgedAt;
        }

        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new MaintenanceAcknowledgement(enforcementRef, acknowledgedAt);
    }

    private static string? Serialize(Guid[] ids) => ids.Length == 0 ? null : JsonSerializer.Serialize(ids);

    private static bool SameIds(string? json, Guid[] ids)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ids.Length == 0;
        try
        {
            var stored = JsonSerializer.Deserialize<Guid[]>(json) ?? [];
            return stored.OrderBy(x => x).SequenceEqual(ids.OrderBy(x => x));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}