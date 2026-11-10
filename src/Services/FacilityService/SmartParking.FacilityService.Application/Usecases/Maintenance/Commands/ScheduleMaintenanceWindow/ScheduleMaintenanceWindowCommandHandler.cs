using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ScheduleMaintenanceWindow;

public sealed class ScheduleMaintenanceWindowCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IOwnerAuthorizer owners,
    IMaintenanceWindowStore store,
    ILogger<ScheduleMaintenanceWindowCommandHandler> logger)
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
        MaintenanceWindowRules.ValidateCreate(null, null, request.Reason, start, end, DateTime.UtcNow);

        return await store.CreateAsync(owner, request.SiteId, start, end, request.Reason.Trim(), ct);
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
