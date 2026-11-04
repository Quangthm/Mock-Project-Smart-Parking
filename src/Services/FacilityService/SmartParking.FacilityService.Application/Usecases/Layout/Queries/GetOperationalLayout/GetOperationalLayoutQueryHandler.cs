using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Layout.Queries.GetOperationalLayout;

public sealed class GetOperationalLayoutQueryHandler(
    ILayoutCallerResolver callerResolver,
    IOperationalLayoutReader operationalLayouts,
    ILogger<GetOperationalLayoutQueryHandler> logger)
    : IRequestHandler<GetOperationalLayoutQuery, OperationalLayoutProjectionDto>
{
    public async Task<OperationalLayoutProjectionDto> Handle(GetOperationalLayoutQuery request, CancellationToken ct)
    {
        logger.LogInformation("Reading operational layout for site {SiteId} (VehicleType: {VehicleType}, Floor: {Floor})",
            request.SiteId, request.VehicleType, request.Floor);

        var caller = await callerResolver.ResolveAsync(request.SiteId, ct);
        var visibility = caller.IsSiteTelemetryGranted
            ? LayoutVisibility.Telemetry
            : LayoutVisibility.Sanitized;
        var scope = ScopeFor(caller);

        // This read path never takes an edit hold or a ReservationService snapshot; the
        // projection reads parking_slots.reservation_state directly.
        OperationalLayoutReadResult result;
        try
        {
            result = await operationalLayouts.ReadAsync(scope, request.SiteId, request.VehicleType, request.Floor, ct);
        }
        catch (StructureException error) when (
            caller.Kind == LayoutCallerKind.Owner &&
            caller.TenantIds.Count > 0 &&
            error.Code == "SITE_NOT_FOUND")
        {
            // An owner scoped to another tenant still gets the sanitized public view.
            result = await operationalLayouts.ReadAsync(
                new LayoutReadScope(null, false), request.SiteId, request.VehicleType, request.Floor, ct);
            visibility = LayoutVisibility.Sanitized;
        }

        // Single choke point: this is the only projection return and it is always redacted.
        return OperationalLayoutRedaction.Apply(
            result.Layout,
            visibility,
            result.MaskedReservedCount,
            result.MaskedZoneReservedCounts);
    }

    private static LayoutReadScope ScopeFor(LayoutCaller caller) => caller.Kind switch
    {
        LayoutCallerKind.Admin => new(null, true),
        LayoutCallerKind.Owner when caller.TenantIds.Count > 0 => new(caller.TenantIds, true),
        LayoutCallerKind.Owner => new(null, false),
        LayoutCallerKind.Operator when caller.IsSiteTelemetryGranted && caller.TenantIds.Count > 0
            => new(caller.TenantIds, true),
        _ => new(null, false)
    };
}
