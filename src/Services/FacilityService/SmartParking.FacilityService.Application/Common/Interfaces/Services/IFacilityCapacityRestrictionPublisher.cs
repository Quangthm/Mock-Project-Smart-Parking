using SmartParking.Contracts.Facility;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Services;

public interface IFacilityCapacityRestrictionPublisher
{
    Task StageRestrictedAsync(FacilityCapacityRestrictedV1 message, CancellationToken ct = default);
    Task StageReleasedAsync(FacilityCapacityRestrictionReleasedV1 message, CancellationToken ct = default);
}