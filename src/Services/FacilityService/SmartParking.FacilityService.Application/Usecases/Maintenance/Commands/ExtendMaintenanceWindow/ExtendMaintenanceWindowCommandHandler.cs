using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ExtendMaintenanceWindow;

public sealed class ExtendMaintenanceWindowCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IOwnerAuthorizer owners,
    IMaintenanceWindowStore store,
    ILogger<ExtendMaintenanceWindowCommandHandler> logger,
    IMaintenanceEnforcementClient? enforcement = null)
    : IRequestHandler<ExtendMaintenanceWindowCommand, MaintenanceWindow>
{
    public async Task<MaintenanceWindow> Handle(ExtendMaintenanceWindowCommand request, CancellationToken ct)
    {
        logger.LogInformation("Extending maintenance window {WindowId} for site {SiteId}",
            request.WindowId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        if (!await owners.IsOwnerAsync(owner, ct))
            throw new StructureException("FORBIDDEN", "Current parking management access is required.");

        var start = request.Start is { } from ? ToUtc(from) : (DateTime?)null;
        var end = request.End is { } to ? ToUtc(to) : (DateTime?)null;

        var current = await store.GetByIdAsync(owner, request.SiteId, request.WindowId, ct)
            ?? throw new StructureException("STRUCTURE_CONFLICT", "Maintenance window was not found in this site.");

        var newEnd = MaintenanceWindowRules.Extend(current.Start, current.End, start, end, DateTime.UtcNow);

        // Captured before the extension so a refused acknowledgement can restore the previous state.
        var previousEnd = current.End;
        var previousState = current.EnforcementState;
        var previousRef = current.EnforcementRef;
        var previousAcknowledgedAt = current.AcknowledgedAt;

        // Phase 1: commit the longer window locally as `pending`, outside any remote call.
        var extended = await store.ExtendAsync(owner, request.SiteId, request.WindowId, newEnd, ct);
        if (enforcement is null) return extended;

        // Phase 2: re-acknowledge the wider interval with Operations.
        var scope = await store.ResolveScopeAsync(owner.TenantId, request.SiteId, extended.UnitId, extended.SlotId, ct);
        var result = await enforcement.ApplyAsync(extended, scope, ct);
        switch (result)
        {
            case MaintenanceEnforcementResult.Acknowledged acknowledged:
                return await store.MarkAcknowledgedAsync(owner.TenantId, request.SiteId, extended.Id,
                    acknowledged.EnforcementRef, acknowledged.AcknowledgedAt, ct);
            case MaintenanceEnforcementResult.Conflict conflict:
                // The wider interval overlaps live commitments: restore the previous window and rethrow.
                await store.RevertExtensionAsync(owner.TenantId, request.SiteId, request.WindowId, previousEnd,
                    previousState, previousRef, previousAcknowledgedAt, ct);
                throw new MaintenanceConflictException(conflict.Commitments, conflict.Message);
            default:
                return extended;
        }
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
