using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ScheduleMaintenanceWindow;

public sealed class ScheduleMaintenanceWindowCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IOwnerAuthorizer owners,
    IMaintenanceWindowStore store,
    ILogger<ScheduleMaintenanceWindowCommandHandler> logger,
    IMaintenanceEnforcementClient? enforcement = null)
    : IRequestHandler<ScheduleMaintenanceWindowCommand, MaintenanceWindow>
{
    public async Task<MaintenanceWindow> Handle(ScheduleMaintenanceWindowCommand request, CancellationToken ct)
    {
        logger.LogInformation("Scheduling maintenance window for site {SiteId} from {Start} to {End}",
            request.SiteId, request.Start, request.End);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        if (!await owners.IsOwnerAsync(owner, ct))
            throw new StructureException("FORBIDDEN", "Current parking management access is required.");

        var start = ToUtc(request.Start);
        var end = ToUtc(request.End);
        MaintenanceWindowRules.ValidateCreate(request.UnitId, request.SlotId, request.Reason, start, end,
            DateTime.UtcNow);

        // Phase 1: the row is committed locally as `pending`. No transaction may stay open across the
        // remote acknowledgement below, so an unreachable Operations service cannot hold local locks.
        var created = await store.CreateAsync(owner, request.SiteId, start, end, request.Reason.Trim(), ct,
            request.UnitId, request.SlotId);
        if (enforcement is null) return created;

        // Phase 2: ask Operations to enforce the window and only then mark it acknowledged.
        var scope = await store.ResolveScopeAsync(owner.TenantId, request.SiteId, created.UnitId, created.SlotId, ct);
        var result = await enforcement.ApplyAsync(created, scope, ct);
        switch (result)
        {
            case MaintenanceEnforcementResult.Acknowledged acknowledged:
                return await store.MarkAcknowledgedAsync(owner.TenantId, request.SiteId, created.Id,
                    acknowledged.EnforcementRef, acknowledged.AcknowledgedAt, ct);
            case MaintenanceEnforcementResult.Conflict conflict:
                // Operations refused because live commitments overlap: drop the pending row so the
                // rejected window never becomes effective, and surface the affected commitments.
                await store.DeletePendingAsync(owner.TenantId, request.SiteId, created.Id, ct);
                throw new MaintenanceConflictException(conflict.Commitments, conflict.Message);
            default:
                // Unavailable: leave the row pending for MaintenanceEnforcementWorker to retry.
                return created;
        }
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
