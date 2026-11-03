namespace SmartParking.FacilityService.Application;

public enum LayoutCallerKind { Anonymous, Driver, Operator, Owner, Admin }
public enum LayoutVisibility { Sanitized, Telemetry }

public sealed record LayoutCaller(
    LayoutCallerKind Kind,
    Guid? UserId,
    IReadOnlyList<Guid> TenantIds,
    bool IsSiteTelemetryGranted);

public interface ILayoutCallerResolver
{
    Task<LayoutCaller> ResolveAsync(Guid siteId, CancellationToken ct);
}
