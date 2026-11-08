using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Application.DTOs;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

// Null TenantIds means no tenant restriction; the reader derives the site tenant from the site row.
public sealed record LayoutReadScope(IReadOnlyList<Guid>? TenantIds, bool IncludeInactiveSites);

// Non-wire plumbing: masked counts are computed by the reader over the unfiltered slot set.
public sealed record OperationalLayoutReadResult(
    OperationalLayoutProjectionDto Layout,
    int MaskedReservedCount,
    IReadOnlyDictionary<string, int> MaskedZoneReservedCounts);

// Projection reader is scope-scoped. Authorization stays in the Application service.
public interface IOperationalLayoutReader
{
    Task<OperationalLayoutReadResult> ReadAsync(
        LayoutReadScope scope,
        Guid siteId,
        VehicleType? vehicleType,
        string? floor,
        CancellationToken cancellationToken);
}
