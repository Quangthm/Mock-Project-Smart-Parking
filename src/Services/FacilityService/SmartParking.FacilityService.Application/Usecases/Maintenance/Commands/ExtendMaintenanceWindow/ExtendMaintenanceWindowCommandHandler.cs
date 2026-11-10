using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ExtendMaintenanceWindow;

public sealed class ExtendMaintenanceWindowCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IOwnerAuthorizer owners,
    IMaintenanceWindowStore store,
    ILogger<ExtendMaintenanceWindowCommandHandler> logger)
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

        return await store.ExtendAsync(owner, request.SiteId, request.WindowId, newEnd, ct);
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
