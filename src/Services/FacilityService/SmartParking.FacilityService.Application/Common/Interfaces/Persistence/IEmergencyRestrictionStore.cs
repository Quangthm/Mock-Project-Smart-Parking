using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public interface IEmergencyRestrictionStore
{
    Task<IReadOnlyList<EmergencyRestriction>> ListActiveAsync(
        Guid tenantId,
        Guid siteId,
        CancellationToken ct);

    Task<EmergencyRestriction> CreateAsync(
        Guid tenantId,
        Guid siteId,
        Guid actorId,
        string actorRole,
        string mode,
        string scope,
        Guid? unitId,
        Guid? slotId,
        string reason,
        DateTimeOffset? effectiveUntil,
        CancellationToken ct);

    Task<EmergencyRestriction?> ReleaseAsync(
        Guid tenantId,
        Guid siteId,
        Guid restrictionId,
        Guid actorId,
        string actorRole,
        CancellationToken ct);

    // Closes every restriction whose `effective_until` has passed, restores the bays it closed and
    // stages the release event, so a timed closure never lingers in physical state after it expires.
    Task<int> ExpireDueAsync(DateTimeOffset now, CancellationToken ct);
}